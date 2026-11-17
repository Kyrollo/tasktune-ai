/*
  TaskTune communications approval/outbox migration (SQL Server).
  Creates auditable approval requests and recipient-specific delivery outbox rows.
  Safe to execute more than once. Backup the database before applying.
*/
IF OBJECT_ID(N'dbo.CommunicationApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationApprovals
    (
        ApprovalId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommunicationApprovals PRIMARY KEY,
        CompanyId INT NOT NULL,
        CommunicationId INT NOT NULL,
        RequestedByUserId INT NOT NULL,
        ReviewedByUserId INT NULL,
        IdempotencyKey NVARCHAR(128) NOT NULL,
        RecipientType NVARCHAR(40) NOT NULL,
        RecipientCount INT NOT NULL,
        Status TINYINT NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL,
        ReviewedAt DATETIME2(3) NULL,
        CompletedAt DATETIME2(3) NULL,
        ReviewNote NVARCHAR(500) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationApprovals_Company FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_CommunicationApprovals_Communication FOREIGN KEY (CommunicationId)
            REFERENCES dbo.CommunicationLog(CommunicationId) ON DELETE CASCADE,
        CONSTRAINT FK_CommunicationApprovals_RequestedBy FOREIGN KEY (RequestedByUserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_CommunicationApprovals_ReviewedBy FOREIGN KEY (ReviewedByUserId)
            REFERENCES dbo.Users(UserId)
    );

    CREATE UNIQUE INDEX UX_CommunicationApprovals_IdempotencyKey
        ON dbo.CommunicationApprovals(IdempotencyKey);
    CREATE UNIQUE INDEX UX_CommunicationApprovals_CommunicationId
        ON dbo.CommunicationApprovals(CommunicationId);
    CREATE INDEX IX_CommunicationApprovals_Company_Status_Created
        ON dbo.CommunicationApprovals(CompanyId, Status, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.CommunicationOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationOutbox
    (
        OutboxId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CommunicationOutbox PRIMARY KEY,
        ApprovalId UNIQUEIDENTIFIER NOT NULL,
        CompanyId INT NOT NULL,
        IdempotencyKey NVARCHAR(160) NOT NULL,
        RecipientEmail NVARCHAR(255) NOT NULL,
        CcEmailsJson NVARCHAR(4000) NOT NULL CONSTRAINT DF_CommunicationOutbox_Cc DEFAULT N'[]',
        Subject NVARCHAR(200) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        Status TINYINT NOT NULL,
        AttemptCount TINYINT NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL,
        StartedAt DATETIME2(3) NULL,
        SentAt DATETIME2(3) NULL,
        LastError NVARCHAR(500) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationOutbox_Approval FOREIGN KEY (ApprovalId)
            REFERENCES dbo.CommunicationApprovals(ApprovalId) ON DELETE CASCADE,
        CONSTRAINT FK_CommunicationOutbox_Company FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies(CompanyId)
    );

    CREATE UNIQUE INDEX UX_CommunicationOutbox_IdempotencyKey
        ON dbo.CommunicationOutbox(IdempotencyKey);
    CREATE INDEX IX_CommunicationOutbox_Approval_Status
        ON dbo.CommunicationOutbox(ApprovalId, Status);
END
GO
