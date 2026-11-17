/*
  TaskTune account-security migration (SQL Server)
  Adds hashed, expiring account tokens and revocable refresh-token session families.
  Safe to execute more than once. Run against the application database after backup.
*/
IF OBJECT_ID(N'dbo.UserSessions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions
    (
        SessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserSessions PRIMARY KEY,
        UserId INT NOT NULL,
        FamilyId UNIQUEIDENTIFIER NOT NULL,
        TokenHash CHAR(64) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL,
        ExpiresAt DATETIME2(3) NOT NULL,
        ConsumedAt DATETIME2(3) NULL,
        RevokedAt DATETIME2(3) NULL,
        ReplacedBySessionId UNIQUEIDENTIFIER NULL,
        UserAgent NVARCHAR(500) NULL,
        IpAddress VARCHAR(45) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_UserSessions_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX UX_UserSessions_TokenHash ON dbo.UserSessions(TokenHash);
    CREATE INDEX IX_UserSessions_UserId_FamilyId ON dbo.UserSessions(UserId, FamilyId);
    CREATE INDEX IX_UserSessions_ExpiresAt ON dbo.UserSessions(ExpiresAt);
END
GO

IF OBJECT_ID(N'dbo.AccountSecurityTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AccountSecurityTokens
    (
        AccountSecurityTokenId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_AccountSecurityTokens PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NULL,
        CreatedByUserId INT NULL,
        Email NVARCHAR(255) NOT NULL,
        Purpose NVARCHAR(40) NOT NULL,
        TokenHash CHAR(64) NOT NULL,
        InvitedRole TINYINT NULL,
        DepartmentId INT NULL,
        PositionId INT NULL,
        CreatedAt DATETIME2(3) NOT NULL,
        ExpiresAt DATETIME2(3) NOT NULL,
        ConsumedAt DATETIME2(3) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_AccountSecurityTokens_Companies FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies(CompanyId) ON DELETE CASCADE,
        CONSTRAINT FK_AccountSecurityTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_AccountSecurityTokens_CreatedBy FOREIGN KEY (CreatedByUserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_AccountSecurityTokens_Departments FOREIGN KEY (DepartmentId)
            REFERENCES dbo.Departments(DepartmentId),
        CONSTRAINT FK_AccountSecurityTokens_Positions FOREIGN KEY (PositionId)
            REFERENCES dbo.Positions(PositionId)
    );

    CREATE UNIQUE INDEX UX_AccountSecurityTokens_TokenHash ON dbo.AccountSecurityTokens(TokenHash);
    CREATE INDEX IX_AccountSecurityTokens_Company_Purpose_Email
        ON dbo.AccountSecurityTokens(CompanyId, Purpose, Email);
    CREATE INDEX IX_AccountSecurityTokens_ExpiresAt
        ON dbo.AccountSecurityTokens(ExpiresAt);
END
GO
