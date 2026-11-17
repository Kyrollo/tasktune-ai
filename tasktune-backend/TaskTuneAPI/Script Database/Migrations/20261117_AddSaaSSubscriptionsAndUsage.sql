/*
  TaskTune SaaS billing foundation.
  Additive migration only. This establishes plan entitlements and usage metering;
  it does NOT collect payments or replace a payment provider webhook integration.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SubscriptionPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionPlans
    (
        SubscriptionPlanId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubscriptionPlans PRIMARY KEY,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        Description NVARCHAR(1000) NOT NULL,
        MaxActiveEmployees INT NULL,
        MaxStorageBytes BIGINT NULL,
        MaxMonthlyAiRequests INT NULL,
        EnabledModulesJson NVARCHAR(MAX) NOT NULL,
        BillingMode NVARCHAR(30) NOT NULL CONSTRAINT DF_SubscriptionPlans_BillingMode DEFAULT N'contact_sales',
        PriceMinorUnits INT NULL,
        CurrencyCode NVARCHAR(3) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_IsActive DEFAULT 1,
        IsPublic BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_IsPublic DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_SubscriptionPlans_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_SubscriptionPlans_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_SubscriptionPlans_Code UNIQUE (Code),
        CONSTRAINT CK_SubscriptionPlans_Quotas CHECK (
            (MaxActiveEmployees IS NULL OR MaxActiveEmployees >= 1) AND
            (MaxStorageBytes IS NULL OR MaxStorageBytes >= 1) AND
            (MaxMonthlyAiRequests IS NULL OR MaxMonthlyAiRequests >= 0)
        ),
        CONSTRAINT CK_SubscriptionPlans_BillingMode CHECK (BillingMode IN (N'free', N'contact_sales', N'provider_managed'))
    );
END;

IF OBJECT_ID(N'dbo.CompanySubscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanySubscriptions
    (
        CompanySubscriptionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CompanySubscriptions PRIMARY KEY,
        CompanyId INT NOT NULL,
        SubscriptionPlanId INT NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_CompanySubscriptions_Status DEFAULT N'active',
        TrialEndsAt DATETIME2(7) NULL,
        CurrentPeriodStart DATETIME2(7) NOT NULL,
        CurrentPeriodEnd DATETIME2(7) NOT NULL,
        CancelAtPeriodEnd BIT NOT NULL CONSTRAINT DF_CompanySubscriptions_CancelAtPeriodEnd DEFAULT 0,
        CancelledAt DATETIME2(7) NULL,
        ExternalCustomerId NVARCHAR(200) NULL,
        ExternalSubscriptionId NVARCHAR(200) NULL,
        BillingProvider NVARCHAR(40) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_CompanySubscriptions_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_CompanySubscriptions_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CompanySubscriptions_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_CompanySubscriptions_Plans FOREIGN KEY (SubscriptionPlanId) REFERENCES dbo.SubscriptionPlans(SubscriptionPlanId),
        CONSTRAINT UQ_CompanySubscriptions_Company UNIQUE (CompanyId),
        CONSTRAINT CK_CompanySubscriptions_Status CHECK ([Status] IN (N'trialing', N'active', N'past_due', N'cancelled', N'suspended')),
        CONSTRAINT CK_CompanySubscriptions_Period CHECK (CurrentPeriodEnd > CurrentPeriodStart)
    );
END;

IF OBJECT_ID(N'dbo.TenantUsageEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantUsageEvents
    (
        TenantUsageEventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantUsageEvents PRIMARY KEY,
        CompanyId INT NOT NULL,
        MeterKey NVARCHAR(60) NOT NULL,
        Quantity DECIMAL(18,4) NOT NULL,
        IdempotencyKey NVARCHAR(160) NOT NULL,
        ResourceType NVARCHAR(80) NULL,
        ResourceId NVARCHAR(160) NULL,
        MetadataJson NVARCHAR(4000) NULL,
        OccurredAt DATETIME2(7) NOT NULL CONSTRAINT DF_TenantUsageEvents_OccurredAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantUsageEvents_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT CK_TenantUsageEvents_Quantity CHECK (Quantity > 0),
        CONSTRAINT UQ_TenantUsageEvents_Idempotency UNIQUE (CompanyId, IdempotencyKey)
    );
    CREATE INDEX IX_TenantUsageEvents_Meter ON dbo.TenantUsageEvents(CompanyId, MeterKey, OccurredAt);
END;

/* Price values are intentionally not guessed. Commercial prices should be configured with the billing provider. */
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'starter')
    INSERT dbo.SubscriptionPlans
        (Code, Name, Description, MaxActiveEmployees, MaxStorageBytes, MaxMonthlyAiRequests, EnabledModulesJson, BillingMode, IsActive, IsPublic)
    VALUES
        (N'starter', N'Starter', N'Essential HR and work management for small teams.', 15, 5368709120, 100,
         N'["core_hr","employee_self_service","leave","attendance","tasks","basic_reports"]', N'free', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'growth')
    INSERT dbo.SubscriptionPlans
        (Code, Name, Description, MaxActiveEmployees, MaxStorageBytes, MaxMonthlyAiRequests, EnabledModulesJson, BillingMode, IsActive, IsPublic)
    VALUES
        (N'growth', N'Growth', N'Advanced workforce operations and reporting.', 100, 53687091200, 3000,
         N'["core_hr","employee_self_service","leave","attendance","tasks","projects","recruitment","performance","workflows","reports","ai_assistant"]', N'contact_sales', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'business')
    INSERT dbo.SubscriptionPlans
        (Code, Name, Description, MaxActiveEmployees, MaxStorageBytes, MaxMonthlyAiRequests, EnabledModulesJson, BillingMode, IsActive, IsPublic)
    VALUES
        (N'business', N'Business', N'Complete HR operations and AI-powered workforce management.', 500, 268435456000, 20000,
         N'["core_hr","employee_self_service","leave","attendance","tasks","projects","recruitment","performance","workflows","reports","advanced_analytics","ai_assistant","integrations","expenses","assets"]', N'contact_sales', 1, 1);

IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlans WHERE Code = N'enterprise')
    INSERT dbo.SubscriptionPlans
        (Code, Name, Description, MaxActiveEmployees, MaxStorageBytes, MaxMonthlyAiRequests, EnabledModulesJson, BillingMode, IsActive, IsPublic)
    VALUES
        (N'enterprise', N'Enterprise', N'Configurable enterprise deployment, integrations and dedicated services.', NULL, NULL, NULL,
         N'["*"]', N'contact_sales', 1, 1);

/* Existing tenants receive the free Starter plan until assigned a paid plan by the platform operator. */
INSERT dbo.CompanySubscriptions
    (CompanyId, SubscriptionPlanId, [Status], CurrentPeriodStart, CurrentPeriodEnd, CreatedAt, UpdatedAt)
SELECT c.CompanyId, p.SubscriptionPlanId, N'active',
       SYSUTCDATETIME(), DATEADD(year, 100, SYSUTCDATETIME()), SYSUTCDATETIME(), SYSUTCDATETIME()
FROM dbo.Companies c
CROSS JOIN dbo.SubscriptionPlans p
WHERE p.Code = N'starter'
  AND NOT EXISTS (SELECT 1 FROM dbo.CompanySubscriptions s WHERE s.CompanyId = c.CompanyId);

COMMIT TRANSACTION;
