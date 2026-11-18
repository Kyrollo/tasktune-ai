/*
 P2 tenant lifecycle. Existing tenants default to Active. Safe to rerun.
*/
IF OBJECT_ID(N'dbo.Companies', N'U') IS NULL
    THROW 51010, 'Companies table must exist before applying tenant lifecycle migration.', 1;
GO
IF COL_LENGTH(N'dbo.Companies', N'Status') IS NULL
BEGIN
    ALTER TABLE dbo.Companies ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Companies_Status DEFAULT (N'Active'), SuspendedAt DATETIME2(3) NULL;
END
ELSE IF COL_LENGTH(N'dbo.Companies', N'SuspendedAt') IS NULL
    ALTER TABLE dbo.Companies ADD SuspendedAt DATETIME2(3) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Companies') AND name = N'CK_Companies_Status_Suspension')
    ALTER TABLE dbo.Companies ADD CONSTRAINT CK_Companies_Status_Suspension CHECK (
        Status IN (N'Active', N'Suspended') AND
        ((Status = N'Active' AND SuspendedAt IS NULL) OR (Status = N'Suspended' AND SuspendedAt IS NOT NULL)));
GO
IF OBJECT_ID(N'dbo.TenantLifecycleEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantLifecycleEvents
    (
        EventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantLifecycleEvents PRIMARY KEY,
        CompanyId INT NOT NULL,
        ActorUserId INT NOT NULL,
        Action NVARCHAR(20) NOT NULL,
        PreviousStatus NVARCHAR(20) NOT NULL,
        NewStatus NVARCHAR(20) NOT NULL,
        Reason NVARCHAR(1000) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TenantLifecycleEvents_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantLifecycleEvents_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_TenantLifecycleEvents_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_TenantLifecycleEvents_Transition CHECK (
            (Action = N'Suspended' AND PreviousStatus = N'Active' AND NewStatus = N'Suspended')
            OR (Action = N'Reactivated' AND PreviousStatus = N'Suspended' AND NewStatus = N'Active'))
    );
    CREATE INDEX IX_TenantLifecycleEvents_Company_CreatedAt ON dbo.TenantLifecycleEvents(CompanyId, CreatedAt DESC);
END
GO
