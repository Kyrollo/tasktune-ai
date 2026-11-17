/*
  Adds atomic-claim lease metadata for the communication outbox.
  Safe to run repeatedly. External dispatch remains disabled by default.
*/
IF OBJECT_ID(N'dbo.CommunicationOutbox', N'U') IS NULL
    THROW 51000, 'CommunicationOutbox must exist before applying the lease migration.', 1;
GO

IF COL_LENGTH(N'dbo.CommunicationOutbox', N'LeaseToken') IS NULL
    ALTER TABLE dbo.CommunicationOutbox ADD LeaseToken UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.CommunicationOutbox', N'LeaseExpiresAt') IS NULL
    ALTER TABLE dbo.CommunicationOutbox ADD LeaseExpiresAt DATETIME2(3) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CommunicationOutbox')
      AND name = N'IX_CommunicationOutbox_Claim'
)
    CREATE INDEX IX_CommunicationOutbox_Claim
        ON dbo.CommunicationOutbox(Status, NextAttemptAt, LeaseExpiresAt, OutboxId);
GO
