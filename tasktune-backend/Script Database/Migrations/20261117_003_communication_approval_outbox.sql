/*
  Communication approval and transactional outbox.
  Apply after backup with the application offline or during a controlled deployment.
*/
IF OBJECT_ID(N'dbo.CommunicationApprovals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationApprovals
    (
        ApprovalId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommunicationApprovals PRIMARY KEY,
        BatchId UNIQUEIDENTIFIER NOT NULL,
        CompanyId INT NOT NULL,
        RequestedByUserId INT NOT NULL,
        Status NVARCHAR(32) NOT NULL CONSTRAINT DF_CommunicationApprovals_Status DEFAULT N'PendingApproval',
        RequestedAt DATETIME2(3) NOT NULL,
        ReviewedByUserId INT NULL,
        ReviewedAt DATETIME2(3) NULL,
        ReviewNote NVARCHAR(1000) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationApprovals_Companies FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies(CompanyId) ON DELETE CASCADE,
        CONSTRAINT FK_CommunicationApprovals_RequestedBy FOREIGN KEY (RequestedByUserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_CommunicationApprovals_ReviewedBy FOREIGN KEY (ReviewedByUserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_CommunicationApprovals_Status CHECK
            (Status IN (N'PendingApproval', N'Approved', N'Rejected'))
    );
    CREATE UNIQUE INDEX UX_CommunicationApprovals_BatchId ON dbo.CommunicationApprovals(BatchId);
    CREATE INDEX IX_CommunicationApprovals_Company_Status_RequestedAt
        ON dbo.CommunicationApprovals(CompanyId, Status, RequestedAt);
END
GO

IF OBJECT_ID(N'dbo.CommunicationOutbox', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommunicationOutbox
    (
        OutboxId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommunicationOutbox PRIMARY KEY,
        CompanyId INT NOT NULL,
        ApprovalId INT NOT NULL,
        CommunicationId INT NOT NULL,
        RecipientUserId INT NOT NULL,
        RecipientEmail NVARCHAR(255) NOT NULL,
        CcEmailsJson NVARCHAR(MAX) NULL,
        Subject NVARCHAR(200) NOT NULL,
        Body NVARCHAR(20000) NOT NULL,
        Status NVARCHAR(32) NOT NULL CONSTRAINT DF_CommunicationOutbox_Status DEFAULT N'PendingApproval',
        AttemptCount INT NOT NULL CONSTRAINT DF_CommunicationOutbox_AttemptCount DEFAULT 0,
        CreatedAt DATETIME2(3) NOT NULL,
        AvailableAt DATETIME2(3) NOT NULL,
        LockedUntil DATETIME2(3) NULL,
        LockId UNIQUEIDENTIFIER NULL,
        SentAt DATETIME2(3) NULL,
        LastAttemptAt DATETIME2(3) NULL,
        LastError NVARCHAR(1000) NULL,
        IdempotencyKey UNIQUEIDENTIFIER NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_CommunicationOutbox_Companies FOREIGN KEY (CompanyId)
            REFERENCES dbo.Companies(CompanyId) ON DELETE CASCADE,
        CONSTRAINT FK_CommunicationOutbox_Approvals FOREIGN KEY (ApprovalId)
            REFERENCES dbo.CommunicationApprovals(ApprovalId),
        CONSTRAINT FK_CommunicationOutbox_Communications FOREIGN KEY (CommunicationId)
            REFERENCES dbo.CommunicationLog(CommunicationId),
        CONSTRAINT FK_CommunicationOutbox_Recipient FOREIGN KEY (RecipientUserId)
            REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_CommunicationOutbox_Status CHECK
            (Status IN (N'PendingApproval', N'Queued', N'Processing', N'Sent', N'Failed', N'Rejected'))
    );
    CREATE UNIQUE INDEX UX_CommunicationOutbox_IdempotencyKey
        ON dbo.CommunicationOutbox(IdempotencyKey);
    CREATE INDEX IX_CommunicationOutbox_Status_AvailableAt_LockedUntil
        ON dbo.CommunicationOutbox(Status, AvailableAt, LockedUntil);
    CREATE INDEX IX_CommunicationOutbox_Company_Approval
        ON dbo.CommunicationOutbox(CompanyId, ApprovalId, Status);
    CREATE UNIQUE INDEX UX_CommunicationOutbox_CommunicationId
        ON dbo.CommunicationOutbox(CommunicationId);
END
GO
