/*
  TaskTune migration: individual employee permissions and reporting lines.
  Safe to run repeatedly. It does not drop or rebuild existing customer tables.
  Review and back up the target database before applying in production.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.UserPermissionOverrides', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserPermissionOverrides
    (
        PermissionOverrideId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserPermissionOverrides PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NOT NULL,
        Resource NVARCHAR(100) NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        CanPerform BIT NOT NULL,
        [Scope] NVARCHAR(20) NOT NULL CONSTRAINT DF_UserPermissionOverrides_Scope DEFAULT N'self',
        ScopeResourceId INT NULL,
        ExpiresAt DATETIME2(7) NULL,
        Reason NVARCHAR(500) NULL,
        GrantedBy INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_UserPermissionOverrides_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_UserPermissionOverrides_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_UserPermissionOverrides_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_UserPermissionOverrides_GrantedBy FOREIGN KEY (GrantedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_UserPermissionOverrides_ScopeResource FOREIGN KEY (ScopeResourceId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_UserPermissionOverrides_Scope CHECK ([Scope] IN (N'self', N'team', N'department', N'company', N'selected'))
    );
    CREATE UNIQUE INDEX UQ_UserPermissionOverrides
        ON dbo.UserPermissionOverrides(CompanyId, UserId, Resource, [Action], ScopeResourceId);
    CREATE INDEX IX_UserPermissionOverrides_Active
        ON dbo.UserPermissionOverrides(CompanyId, UserId, ExpiresAt);
END;

IF OBJECT_ID(N'dbo.EmployeeReportingLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeReportingLines
    (
        ReportingLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeReportingLines PRIMARY KEY,
        CompanyId INT NOT NULL,
        EmployeeUserId INT NOT NULL,
        ManagerUserId INT NOT NULL,
        RelationshipType NVARCHAR(30) NOT NULL CONSTRAINT DF_EmployeeReportingLines_Type DEFAULT N'line',
        IsPrimary BIT NOT NULL CONSTRAINT DF_EmployeeReportingLines_IsPrimary DEFAULT 0,
        EffectiveFrom DATE NOT NULL,
        EffectiveTo DATE NULL,
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_EmployeeReportingLines_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_EmployeeReportingLines_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_EmployeeReportingLines_Employee FOREIGN KEY (EmployeeUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_EmployeeReportingLines_Manager FOREIGN KEY (ManagerUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_EmployeeReportingLines_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_EmployeeReportingLines_NotSelf CHECK (EmployeeUserId <> ManagerUserId),
        CONSTRAINT CK_EmployeeReportingLines_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom),
        CONSTRAINT CK_EmployeeReportingLines_Type CHECK (RelationshipType IN (N'line', N'matrix', N'project', N'mentor'))
    );
    CREATE UNIQUE INDEX UQ_EmployeeReportingLines
        ON dbo.EmployeeReportingLines(CompanyId, EmployeeUserId, ManagerUserId, RelationshipType, EffectiveFrom);
    CREATE INDEX IX_EmployeeReportingLines_Employee
        ON dbo.EmployeeReportingLines(CompanyId, EmployeeUserId, EffectiveTo);
    CREATE INDEX IX_EmployeeReportingLines_Manager
        ON dbo.EmployeeReportingLines(CompanyId, ManagerUserId, EffectiveTo);
END;

COMMIT TRANSACTION;
