/*
  Compatibility migration for SaaS storage usage deltas.
  Allows negative storage_bytes events for file deletion/reconciliation only.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.TenantUsageEvents', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.TenantUsageEvents')
          AND name = N'CK_TenantUsageEvents_Quantity'
    )
    BEGIN
        ALTER TABLE dbo.TenantUsageEvents DROP CONSTRAINT CK_TenantUsageEvents_Quantity;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.TenantUsageEvents')
          AND name = N'CK_TenantUsageEvents_Quantity'
    )
    BEGIN
        ALTER TABLE dbo.TenantUsageEvents
        ADD CONSTRAINT CK_TenantUsageEvents_Quantity
        CHECK
        (
            (MeterKey = N'storage_bytes' AND Quantity <> 0)
            OR
            (MeterKey <> N'storage_bytes' AND Quantity > 0)
        );
    END;
END;

COMMIT TRANSACTION;
