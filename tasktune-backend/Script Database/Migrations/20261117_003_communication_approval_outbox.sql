/*
  TaskTune communication approvals and durable delivery outbox.
  Safe to run repeatedly. Back up the application database before applying.
  External email remains disabled until a separately reviewed dispatcher is deployed.
*/
IF OBJECT_ID(N'dbo.CommunicationApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationApprovals
    (
        ApprovalId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommunicationApprovals PRIMARY KEY,
        CompanyId INT NOT NULL,
        RequestedByUserId INT NOT NULL,
        ReviewedByUserId INT NULL,
        CommunicationType TINYINT NOT NULL,
        RecipientType NVARCHAR(30) NOT NULL,
        RecipientEmailsJson NVARCHAR(MAX) NOT NULL,
        CcEmailsJson NVARCHAR(MAX) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CommunicationApprovals_Status DEFAULT N'Pending',
        IdempotencyKey UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_CommunicationApprovals_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReviewedAt DATETIME2(3) NULL,
        RejectionReason NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationApprovals_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_CommunicationApprovals_RequestedBy FOREIGN KEY (RequestedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_CommunicationApprovals_ReviewedBy FOREIGN KEY (ReviewedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_CommunicationApprovals_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected'))
    );
    CREATE UNIQUE INDEX UX_CommunicationApprovals_Company_Idempotency ON dbo.CommunicationApprovals(CompanyId, IdempotencyKey);
    CREATE UNIQUE INDEX UX_CommunicationApprovals_Id_Company ON dbo.CommunicationApprovals(ApprovalId, CompanyId);
    CREATE INDEX IX_CommunicationApprovals_Company_Status_Created ON dbo.CommunicationApprovals(CompanyId, Status, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.CommunicationOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationOutbox
    (
        OutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommunicationOutbox PRIMARY KEY,
        CompanyId INT NOT NULL,
        ApprovalId INT NOT NULL,
        RecipientEmail NVARCHAR(255) NOT NULL,
        CcEmailsJson NVARCHAR(MAX) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CommunicationOutbox_Status DEFAULT N'Pending',
        Attempts INT NOT NULL CONSTRAINT DF_CommunicationOutbox_Attempts DEFAULT 0,
        NextAttemptAt DATETIME2(3) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_CommunicationOutbox_CreatedAt DEFAULT SYSUTCDATETIME(),
        SentAt DATETIME2(3) NULL,
        LastError NVARCHAR(2000) NULL,
        IdempotencyKey CHAR(64) NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationOutbox_ApprovalTenant FOREIGN KEY (ApprovalId, CompanyId)
            REFERENCES dbo.CommunicationApprovals(ApprovalId, CompanyId),
        CONSTRAINT CK_CommunicationOutbox_Status CHECK (Status IN (N'Pending', N'Processing', N'Sent', N'Failed', N'DeadLetter'))
    );
    CREATE UNIQUE INDEX UX_CommunicationOutbox_IdempotencyKey ON dbo.CommunicationOutbox(IdempotencyKey);
    CREATE INDEX IX_CommunicationOutbox_Company_Status_NextAttempt ON dbo.CommunicationOutbox(CompanyId, Status, NextAttemptAt);
    CREATE INDEX IX_CommunicationOutbox_Approval ON dbo.CommunicationOutbox(ApprovalId, CompanyId);
END
GO
