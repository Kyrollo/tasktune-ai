using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using TaskTune.API.Middleware;
using TaskTune.Application.Interfaces;
using TaskTune.Infrastructure.Models;
using TaskTune.Infrastructure.Services;
using TaskTune.API.Tenancy;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required. Configure it with .NET User Secrets or an environment variable.");
}

var jwtSettings = builder.Configuration.GetRequiredSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:SecretKey must contain at least 32 UTF-8 bytes. Configure a private key with .NET User Secrets or an environment variable.");
}

var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("JwtSettings:Issuer and JwtSettings:Audience are required.");
}

var accessTokenExpirationMinutes = builder.Configuration.GetValue<int?>("JwtSettings:AccessTokenExpirationMinutes") ?? 60;
var refreshTokenExpirationDays = builder.Configuration.GetValue<int?>("JwtSettings:RefreshTokenExpirationDays") ?? 7;
if (accessTokenExpirationMinutes <= 0 || refreshTokenExpirationDays <= 0)
{
    throw new InvalidOperationException("JWT access-token minutes and refresh-token days must be positive values.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

var aiEncryptionKey = builder.Configuration["AISecrets:EncryptionKey"];
if (!builder.Environment.IsDevelopment())
{
    byte[] decodedAiEncryptionKey;
    try
    {
        decodedAiEncryptionKey = Convert.FromBase64String(aiEncryptionKey ?? string.Empty);
    }
    catch (FormatException ex)
    {
        throw new InvalidOperationException("AISecrets:EncryptionKey must be a base64-encoded 32-byte key outside Development.", ex);
    }

    if (decodedAiEncryptionKey.Length != 32)
        throw new InvalidOperationException("AISecrets:EncryptionKey must decode to exactly 32 bytes outside Development.");
}

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tasktune-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddDbContext<TaskTuneDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = signingKey,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        RoleClaimType = "role",
        ClockSkew = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddAuthorization();

var agentServiceApiKey = builder.Configuration["AgentServices:ServiceApiKey"];
if (!builder.Environment.IsDevelopment() &&
    (string.IsNullOrWhiteSpace(agentServiceApiKey) || Encoding.UTF8.GetByteCount(agentServiceApiKey) < 32))
{
    throw new InvalidOperationException("AgentServices:ServiceApiKey must be configured with at least 32 UTF-8 bytes outside Development.");
}

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").GetChildren()
    .Select(item => item.Value?.Trim())
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Cast<string>()
    .ToArray();

if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    corsOrigins = ["http://localhost:5173", "http://localhost:5174", "http://localhost:3000"];
}
else if (corsOrigins.Length == 0)
{
    throw new InvalidOperationException("Cors:AllowedOrigins must contain explicit frontend origins outside Development.");
}

builder.Services.AddCors(options =>
    options.AddPolicy("ReactFrontend", policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskTune API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactFrontend");
app.UseAuthentication();
app.UseMiddleware<TenantMembershipMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();
