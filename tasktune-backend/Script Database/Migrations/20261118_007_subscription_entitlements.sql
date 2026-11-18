/*
 P2 subscription and feature entitlement foundation.
 Existing companies receive a Legacy plan to preserve current AI chat/task-guidance access.
 New tenants without an assigned subscription fail closed on feature-gated actions.
*/
IF OBJECT_ID(N'dbo.Companies', N'U') IS NULL
    THROW 51020, 'Companies table must exist before applying subscription migration.', 1;
GO
IF OBJECT_ID(N'dbo.SubscriptionPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionPlans
    (
        PlanId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubscriptionPlans PRIMARY KEY,
        Code NVARCHAR(64) NOT NULL, Name NVARCHAR(120) NOT NULL, Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_SubscriptionPlans_IsActive DEFAULT (1),
        MaxUsers INT NULL, MaxStorageBytes BIGINT NULL, MonthlyAiTokenLimit BIGINT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_SubscriptionPlans_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_SubscriptionPlans_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_SubscriptionPlans_Code UNIQUE (Code),
        CONSTRAINT CK_SubscriptionPlans_Limits CHECK (
            (MaxUsers IS NULL OR MaxUsers > 0) AND (MaxStorageBytes IS NULL OR MaxStorageBytes > 0)
            AND (MonthlyAiTokenLimit IS NULL OR MonthlyAiTokenLimit >= 0))
    );
END
GO
IF OBJECT_ID(N'dbo.SubscriptionPlanFeatures', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubscriptionPlanFeatures
    (
        PlanId INT NOT NULL, FeatureCode NVARCHAR(100) NOT NULL, IsEnabled BIT NOT NULL, LimitValue INT NULL,
        CONSTRAINT PK_SubscriptionPlanFeatures PRIMARY KEY (PlanId, FeatureCode),
        CONSTRAINT FK_SubscriptionPlanFeatures_Plans FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(PlanId) ON DELETE CASCADE,
        CONSTRAINT CK_SubscriptionPlanFeatures_Limit CHECK (LimitValue IS NULL OR LimitValue >= 0)
    );
END
GO
IF OBJECT_ID(N'dbo.TenantSubscriptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantSubscriptions
    (
        SubscriptionId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantSubscriptions PRIMARY KEY,
        CompanyId INT NOT NULL, PlanId INT NOT NULL, Status NVARCHAR(20) NOT NULL,
        StartsAt DATETIME2(3) NOT NULL, EndsAt DATETIME2(3) NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TenantSubscriptions_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TenantSubscriptions_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UQ_TenantSubscriptions_Company UNIQUE (CompanyId),
        CONSTRAINT FK_TenantSubscriptions_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId) ON DELETE CASCADE,
        CONSTRAINT FK_TenantSubscriptions_Plans FOREIGN KEY (PlanId) REFERENCES dbo.SubscriptionPlans(PlanId),
        CONSTRAINT CK_TenantSubscriptions_Status CHECK (Status IN (N'Trial', N'Active', N'PastDue', N'Cancelled')),
        CONSTRAINT CK_TenantSubscriptions_Period CHECK (EndsAt IS NULL OR EndsAt > StartsAt)
    );
    CREATE INDEX IX_TenantSubscriptions_Plan_Status ON dbo.TenantSubscriptions(PlanId, Status);
END
GO
IF OBJECT_ID(N'dbo.TenantSubscriptionEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantSubscriptionEvents
    (
        EventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantSubscriptionEvents PRIMARY KEY,
        CompanyId INT NOT NULL, ActorUserId INT NOT NULL, PreviousPlanId INT NULL, NewPlanId INT NOT NULL,
        Action NVARCHAR(20) NOT NULL, PreviousStatus NVARCHAR(20) NOT NULL, NewStatus NVARCHAR(20) NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TenantSubscriptionEvents_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantSubscriptionEvents_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_TenantSubscriptionEvents_Actors FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_TenantSubscriptionEvents_NewPlan FOREIGN KEY (NewPlanId) REFERENCES dbo.SubscriptionPlans(PlanId),
        CONSTRAINT FK_TenantSubscriptionEvents_PreviousPlan FOREIGN KEY (PreviousPlanId) REFERENCES dbo.SubscriptionPlans(PlanId),
        CONSTRAINT CK_TenantSubscriptionEvents_Action CHECK (Action IN (N'Assigned', N'PlanChanged', N'StatusChanged'))
    );
    CREATE INDEX IX_TenantSubscriptionEvents_Company_CreatedAt ON dbo.TenantSubscriptionEvents(CompanyId, CreatedAt DESC);
END
GO
DECLARE @LegacyPlanId INT;
SELECT @LegacyPlanId = PlanId FROM dbo.SubscriptionPlans WHERE Code = N'legacy';
IF @LegacyPlanId IS NULL
BEGIN
    INSERT dbo.SubscriptionPlans (Code, Name, Description, IsActive)
    VALUES (N'legacy', N'Legacy', N'Compatibility plan for tenants that existed before plan entitlements.', 1);
    SET @LegacyPlanId = CONVERT(INT, SCOPE_IDENTITY());
END
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlanFeatures WHERE PlanId = @LegacyPlanId AND FeatureCode = N'ai.chat')
    INSERT dbo.SubscriptionPlanFeatures (PlanId, FeatureCode, IsEnabled) VALUES (@LegacyPlanId, N'ai.chat', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.SubscriptionPlanFeatures WHERE PlanId = @LegacyPlanId AND FeatureCode = N'ai.task_guidance')
    INSERT dbo.SubscriptionPlanFeatures (PlanId, FeatureCode, IsEnabled) VALUES (@LegacyPlanId, N'ai.task_guidance', 1);
INSERT dbo.TenantSubscriptions (CompanyId, PlanId, Status, StartsAt)
SELECT company.CompanyId, @LegacyPlanId, N'Active', SYSUTCDATETIME()
FROM dbo.Companies AS company
WHERE NOT EXISTS (SELECT 1 FROM dbo.TenantSubscriptions AS subscription WHERE subscription.CompanyId = company.CompanyId);
GO
