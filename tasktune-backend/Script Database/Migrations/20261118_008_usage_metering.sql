/*
  P2 usage metering v1: append-only, idempotent usage events.
  Active seats and task-attachment storage are calculated from existing tenant tables.
  AI token events are populated only from trusted provider/server usage reports.
*/
IF OBJECT_ID(N'dbo.TenantUsageEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TenantUsageEvents
    (
        EventId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TenantUsageEvents PRIMARY KEY,
        CompanyId INT NOT NULL,
        MeterCode NVARCHAR(64) NOT NULL,
        Quantity BIGINT NOT NULL,
        IdempotencyKey NVARCHAR(128) NOT NULL,
        Source NVARCHAR(100) NOT NULL,
        OccurredAt DATETIME2(3) NOT NULL,
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_TenantUsageEvents_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_TenantUsageEvents_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT CK_TenantUsageEvents_Quantity CHECK (Quantity > 0),
        CONSTRAINT UQ_TenantUsageEvents_Company_Idempotency UNIQUE (CompanyId, IdempotencyKey)
    );
    CREATE INDEX IX_TenantUsageEvents_Company_Meter_Occurred
        ON dbo.TenantUsageEvents(CompanyId, MeterCode, OccurredAt);
END
GO
