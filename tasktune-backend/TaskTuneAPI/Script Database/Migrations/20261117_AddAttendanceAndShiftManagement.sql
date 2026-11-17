/*
  TaskTune Attendance Management foundation.
  - Additive migration; customer data is not dropped or rewritten.
  - Configure company time zone in Company Settings before enabling time-clock usage.
  - Attendance dates are company-local; check-in/out timestamps are stored in UTC.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.WorkShifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkShifts
    (
        WorkShiftId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkShifts PRIMARY KEY,
        CompanyId INT NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        StartTime TIME(0) NOT NULL,
        EndTime TIME(0) NOT NULL,
        BreakMinutes INT NOT NULL CONSTRAINT DF_WorkShifts_BreakMinutes DEFAULT 0,
        GraceMinutes INT NOT NULL CONSTRAINT DF_WorkShifts_GraceMinutes DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_WorkShifts_IsActive DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_WorkShifts_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_WorkShifts_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_WorkShifts_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT UQ_WorkShifts_Company_Name UNIQUE (CompanyId, Name),
        CONSTRAINT CK_WorkShifts_BreakGrace CHECK (BreakMinutes BETWEEN 0 AND 720 AND GraceMinutes BETWEEN 0 AND 180),
        CONSTRAINT CK_WorkShifts_Duration CHECK (StartTime <> EndTime)
    );
END;

IF OBJECT_ID(N'dbo.EmployeeShiftAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeShiftAssignments
    (
        EmployeeShiftAssignmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EmployeeShiftAssignments PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NOT NULL,
        WorkShiftId INT NOT NULL,
        DaysOfWeekMask TINYINT NOT NULL,
        EffectiveFrom DATE NOT NULL,
        EffectiveTo DATE NULL,
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_EmployeeShiftAssignments_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_EmployeeShiftAssignments_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_EmployeeShiftAssignments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_EmployeeShiftAssignments_Shifts FOREIGN KEY (WorkShiftId) REFERENCES dbo.WorkShifts(WorkShiftId),
        CONSTRAINT FK_EmployeeShiftAssignments_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_EmployeeShiftAssignments_Days CHECK (DaysOfWeekMask BETWEEN 1 AND 127),
        CONSTRAINT CK_EmployeeShiftAssignments_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );
    CREATE INDEX IX_EmployeeShiftAssignments_User_Dates
        ON dbo.EmployeeShiftAssignments(CompanyId, UserId, EffectiveFrom, EffectiveTo);
    CREATE INDEX IX_EmployeeShiftAssignments_Shift
        ON dbo.EmployeeShiftAssignments(CompanyId, WorkShiftId, EffectiveTo);
END;

IF OBJECT_ID(N'dbo.AttendanceRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AttendanceRecords
    (
        AttendanceRecordId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttendanceRecords PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NOT NULL,
        WorkShiftId INT NOT NULL,
        WorkDate DATE NOT NULL,
        ScheduledStartUtc DATETIME2(7) NOT NULL,
        ScheduledEndUtc DATETIME2(7) NOT NULL,
        CheckInUtc DATETIME2(7) NULL,
        CheckOutUtc DATETIME2(7) NULL,
        [Status] NVARCHAR(30) NOT NULL CONSTRAINT DF_AttendanceRecords_Status DEFAULT N'scheduled',
        LateMinutes INT NOT NULL CONSTRAINT DF_AttendanceRecords_LateMinutes DEFAULT 0,
        EarlyLeaveMinutes INT NOT NULL CONSTRAINT DF_AttendanceRecords_EarlyLeaveMinutes DEFAULT 0,
        WorkedMinutes INT NOT NULL CONSTRAINT DF_AttendanceRecords_WorkedMinutes DEFAULT 0,
        BreakMinutes INT NOT NULL CONSTRAINT DF_AttendanceRecords_BreakMinutes DEFAULT 0,
        CaptureSource NVARCHAR(20) NOT NULL CONSTRAINT DF_AttendanceRecords_CaptureSource DEFAULT N'web',
        Notes NVARCHAR(2000) NULL,
        IsManualAdjustment BIT NOT NULL CONSTRAINT DF_AttendanceRecords_IsManualAdjustment DEFAULT 0,
        AdjustedBy INT NULL,
        AdjustedAt DATETIME2(7) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_AttendanceRecords_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_AttendanceRecords_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_AttendanceRecords_Shifts FOREIGN KEY (WorkShiftId) REFERENCES dbo.WorkShifts(WorkShiftId),
        CONSTRAINT FK_AttendanceRecords_AdjustedBy FOREIGN KEY (AdjustedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT UQ_AttendanceRecords_Employee_Day UNIQUE (CompanyId, UserId, WorkDate),
        CONSTRAINT CK_AttendanceRecords_Status CHECK ([Status] IN (N'scheduled', N'present', N'late', N'early_leave', N'late_and_early', N'missed', N'on_leave', N'holiday')),
        CONSTRAINT CK_AttendanceRecords_Source CHECK (CaptureSource IN (N'web', N'mobile', N'manual', N'import', N'system')),
        CONSTRAINT CK_AttendanceRecords_Metrics CHECK (LateMinutes >= 0 AND EarlyLeaveMinutes >= 0 AND WorkedMinutes >= 0 AND BreakMinutes >= 0),
        CONSTRAINT CK_AttendanceRecords_Times CHECK (
            ScheduledEndUtc > ScheduledStartUtc AND
            (CheckInUtc IS NULL OR CheckInUtc >= DATEADD(hour, -6, ScheduledStartUtc)) AND
            (CheckOutUtc IS NULL OR (CheckInUtc IS NOT NULL AND CheckOutUtc >= CheckInUtc))
        )
    );
    CREATE INDEX IX_AttendanceRecords_Day_Status
        ON dbo.AttendanceRecords(CompanyId, WorkDate, [Status]);
END;

COMMIT TRANSACTION;
