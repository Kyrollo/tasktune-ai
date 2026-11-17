/*
  Compatibility patch for AttendanceRecords created from older versions
  of the attendance migration that did not include system schedule generation.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.AttendanceRecords', N'U') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.AttendanceRecords')
          AND name = N'CK_AttendanceRecords_Source'
    )
        ALTER TABLE dbo.AttendanceRecords DROP CONSTRAINT CK_AttendanceRecords_Source;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.AttendanceRecords')
          AND name = N'CK_AttendanceRecords_Source'
    )
        ALTER TABLE dbo.AttendanceRecords
        ADD CONSTRAINT CK_AttendanceRecords_Source
        CHECK (CaptureSource IN (N'web', N'mobile', N'manual', N'import', N'system'));
END;

COMMIT TRANSACTION;
