# TaskTune AI — Local Development Setup

This guide describes the repository's development layout: the ASP.NET Core API, SQL Server database, React/Vite frontend, and Python agent services.

## 1. Database setup

The application uses Microsoft SQL Server.

1. Open SQL Server Management Studio or Azure Data Studio.
2. Connect to a local development SQL Server instance, such as LocalDB or SQL Express.
3. Review the script at tasktune-backend/Script Database/setup_database.sql.
4. Execute it only against a disposable development database.

> WARNING: The setup script drops and recreates the TaskTuneAIDatabase database. Back up any data you need and never run it against a production or customer database.

The seed script creates BCrypt-hashed demo accounts. The shared development password is Password123! and must never be reused for a real account or production deployment.

## 2. Backend API (.NET 10)

The active API project is tasktune-backend/TaskTuneAPI/TaskTune.API/TaskTune.API.csproj. The older TaskTuneAPI.csproj project at the TaskTuneAPI directory root targets .NET Framework 4.8 and is not the modern API entry point.

Install the .NET 10 SDK, then open PowerShell and move to the active API directory:

~~~powershell
cd tasktune-backend/TaskTuneAPI/TaskTune.API
~~~

The project has a User Secrets ID configured. Store the SQL connection string locally:

~~~powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=YOUR_SERVER_NAME;Database=TaskTuneAIDatabase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
~~~

Generate a private signing key and store it outside the repository:

~~~powershell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet user-secrets set "JwtSettings:SecretKey" $jwtKey
~~~

The API requires a JWT key of at least 32 UTF-8 bytes and a configured SQL connection string. These values can also be supplied through the environment variables ConnectionStrings__DefaultConnection and JwtSettings__SecretKey.

Generate a separate private API-to-agent key and store it outside source control:

~~~powershell
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$agentKey = [Convert]::ToBase64String($bytes)
$rng.Dispose()
dotnet user-secrets set "AgentServices:ServiceApiKey" $agentKey
~~~

The API requires an explicit `Cors:AllowedOrigins` list outside Development. Configure its exact HTTPS frontend origin (for example `Cors:AllowedOrigins:0=https://app.example.com`); do not use `*` with credentialed requests.

Restore and run the API:

~~~powershell
dotnet restore
$env:ASPNETCORE_URLS = "http://localhost:5000"
dotnet run
~~~

Swagger is enabled only in the Development environment. Use the URL printed by ASP.NET Core at startup.

### Authentication behavior in this security baseline

- Login accepts BCrypt password hashes only. The provided SQL seed users already use BCrypt. Any pre-existing account whose PasswordHash column contains plaintext must go through an administrator-assisted password reset; plaintext fallback is no longer accepted.
- Refresh tokens are returned to the client but only SHA-256 digests are stored in SQL Server. All sessions holding refresh tokens issued before this change must sign in again.
- Public self-service registration currently returns HTTP 503 until tenant-scoped invitations and email verification are implemented.
- Access-token and refresh-token lifetimes are read from JwtSettings in appsettings.json and can be overridden with User Secrets or environment variables.

## 3. Python AI agents

Create a virtual environment and install the requirements:

~~~powershell
cd ../../..
cd tasktune-agents
py -3.12 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
pip install -r requirements.txt
~~~

Copy `.env.example` to `.env` in `tasktune-agents/`. Set `TASKTUNE_SERVICE_API_KEY` to the exact same generated value stored as `AgentServices:ServiceApiKey` for the API. Every agent route except `GET /api/health` rejects requests without this shared service key; wildcard CORS has been removed. Add model-provider credentials to `.env` only for agents that need them. Never commit `.env` or provider keys. Follow tasktune-agents/project_run_guide.md for the available agent startup commands and ports.

## 4. React frontend

Open another terminal:

~~~powershell
cd tasktune-frontend
npm ci
npm run dev
~~~

Vite prints the local web URL when the development server starts (normally http://localhost:5173).

## 5. Regression checks

From the repository root:

~~~powershell
dotnet build tasktune-backend/TaskTuneAPI/TaskTune.API/TaskTune.API.csproj --configuration Release
dotnet test tasktune-backend/TaskTuneAPI/TaskTune.Tests/TaskTune.Tests.csproj --configuration Release
cd tasktune-frontend
npm ci
npm run lint
npm run build
cd ..
python -m compileall -q tasktune-agents
~~~

## 6. Credential hygiene

The tracked appsettings files no longer contain database credentials or a JWT signing key. If credentials were previously committed, removing them from the current tree does not remove them from Git history. Rotate the database credential and JWT signing key before using any shared environment. Never commit real connection strings, signing keys, API keys, or customer data.
