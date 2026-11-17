/*
  TaskTune migration: Leave Management
  - Additive and re-runnable; existing customer tables are not dropped.
  - Back up the database and test this migration on a copy before production.
  - Annual allowance defaults to zero: HR must configure entitlement according to the
    customer's country, contract, employment policy and applicable law.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.LeaveTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveTypes
    (
        LeaveTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveTypes PRIMARY KEY,
        CompanyId INT NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        Name NVARCHAR(120) NOT NULL,
        Description NVARCHAR(1000) NULL,
        IsPaid BIT NOT NULL CONSTRAINT DF_LeaveTypes_IsPaid DEFAULT 1,
        RequiresAttachment BIT NOT NULL CONSTRAINT DF_LeaveTypes_RequiresAttachment DEFAULT 0,
        RequiresApproval BIT NOT NULL CONSTRAINT DF_LeaveTypes_RequiresApproval DEFAULT 1,
        AnnualAllowanceDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveTypes_AnnualAllowance DEFAULT 0,
        MaximumConsecutiveDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveTypes_MaxConsecutive DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_LeaveTypes_IsActive DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveTypes_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveTypes_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LeaveTypes_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT CK_LeaveTypes_Allowances CHECK (AnnualAllowanceDays >= 0 AND MaximumConsecutiveDays >= 0)
    );
    CREATE UNIQUE INDEX UQ_LeaveTypes_Company_Code ON dbo.LeaveTypes(CompanyId, Code);
END;

IF OBJECT_ID(N'dbo.LeaveBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveBalances
    (
        LeaveBalanceId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveBalances PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NOT NULL,
        LeaveTypeId INT NOT NULL,
        [Year] INT NOT NULL,
        AllocatedDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveBalances_Allocated DEFAULT 0,
        CarriedOverDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveBalances_Carried DEFAULT 0,
        UsedDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveBalances_Used DEFAULT 0,
        PendingDays DECIMAL(8,2) NOT NULL CONSTRAINT DF_LeaveBalances_Pending DEFAULT 0,
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveBalances_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_LeaveBalances_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_LeaveBalances_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_LeaveBalances_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(LeaveTypeId),
        CONSTRAINT CK_LeaveBalances_Amounts CHECK (
            AllocatedDays >= 0 AND CarriedOverDays >= 0 AND UsedDays >= 0 AND PendingDays >= 0
        ),
        CONSTRAINT CK_LeaveBalances_Year CHECK ([Year] BETWEEN 2000 AND 2200)
    );
    CREATE UNIQUE INDEX UQ_LeaveBalances_Annual
        ON dbo.LeaveBalances(CompanyId, UserId, LeaveTypeId, [Year]);
END;

IF OBJECT_ID(N'dbo.LeaveRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveRequests
    (
        LeaveRequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequests PRIMARY KEY,
        CompanyId INT NOT NULL,
        UserId INT NOT NULL,
        LeaveTypeId INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        RequestedDays DECIMAL(8,2) NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT N'pending',
        ApproverUserId INT NULL,
        DecisionBy INT NULL,
        DecisionAt DATETIME2(7) NULL,
        DecisionComment NVARCHAR(2000) NULL,
        SubmittedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveRequests_SubmittedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveRequests_UpdatedAt DEFAULT SYSUTCDATETIME(),
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_LeaveRequests_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_LeaveRequests_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_LeaveRequests_LeaveTypes FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveTypes(LeaveTypeId),
        CONSTRAINT FK_LeaveRequests_Approver FOREIGN KEY (ApproverUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_LeaveRequests_DecisionBy FOREIGN KEY (DecisionBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_LeaveRequests_Dates CHECK (EndDate >= StartDate),
        CONSTRAINT CK_LeaveRequests_Days CHECK (RequestedDays > 0),
        CONSTRAINT CK_LeaveRequests_Status CHECK ([Status] IN (N'pending', N'approved', N'rejected', N'cancelled'))
    );
    CREATE INDEX IX_LeaveRequests_Employee_Dates
        ON dbo.LeaveRequests(CompanyId, UserId, StartDate, EndDate);
    CREATE INDEX IX_LeaveRequests_Approver_Status
        ON dbo.LeaveRequests(CompanyId, ApproverUserId, [Status]);
END;

IF OBJECT_ID(N'dbo.CompanyHolidays', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyHolidays
    (
        CompanyHolidayId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CompanyHolidays PRIMARY KEY,
        CompanyId INT NOT NULL,
        Name NVARCHAR(160) NOT NULL,
        HolidayDate DATE NOT NULL,
        IsRecurring BIT NOT NULL CONSTRAINT DF_CompanyHolidays_Recurring DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_CompanyHolidays_Active DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_CompanyHolidays_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CompanyHolidays_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId)
    );
    CREATE INDEX IX_CompanyHolidays_Date ON dbo.CompanyHolidays(CompanyId, HolidayDate);
END;

/* Generic categories only. Jurisdiction-specific entitlements remain customer-configurable. */
INSERT INTO dbo.LeaveTypes
    (CompanyId, Code, Name, Description, IsPaid, RequiresAttachment, RequiresApproval,
     AnnualAllowanceDays, MaximumConsecutiveDays, IsActive)
SELECT c.CompanyId, v.Code, v.Name, v.Description, v.IsPaid, v.RequiresAttachment, 1, 0, 0, 1
FROM dbo.Companies c
CROSS JOIN (VALUES
    (N'annual', N'Annual Leave', N'Paid time off; configure allowance for this company and jurisdiction.', 1, 0),
    (N'sick', N'Sick Leave', N'Sickness-related absence; configure rules and evidence requirements.', 1, 1),
    (N'unpaid', N'Unpaid Leave', N'Unpaid absence; configure approval and eligibility rules.', 0, 0)
) v(Code, Name, Description, IsPaid, RequiresAttachment)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.LeaveTypes lt
    WHERE lt.CompanyId = c.CompanyId AND lt.Code = v.Code
);

IF OBJECT_ID(N'dbo.LeaveRequestBalanceAllocations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveRequestBalanceAllocations
    (
        LeaveRequestBalanceAllocationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequestBalanceAllocations PRIMARY KEY,
        CompanyId INT NOT NULL,
        LeaveRequestId INT NOT NULL,
        LeaveBalanceId INT NOT NULL,
        LeaveYear INT NOT NULL,
        AllocatedDays DECIMAL(8,2) NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_LeaveRequestBalanceAllocations_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LeaveRequestBalanceAllocations_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_LeaveRequestBalanceAllocations_Requests FOREIGN KEY (LeaveRequestId) REFERENCES dbo.LeaveRequests(LeaveRequestId),
        CONSTRAINT FK_LeaveRequestBalanceAllocations_Balances FOREIGN KEY (LeaveBalanceId) REFERENCES dbo.LeaveBalances(LeaveBalanceId),
        CONSTRAINT CK_LeaveRequestBalanceAllocations_Days CHECK (AllocatedDays > 0),
        CONSTRAINT CK_LeaveRequestBalanceAllocations_Year CHECK (LeaveYear BETWEEN 2000 AND 2200)
    );
    CREATE UNIQUE INDEX UQ_LeaveRequestBalanceAllocations
        ON dbo.LeaveRequestBalanceAllocations(CompanyId, LeaveRequestId, LeaveBalanceId);
END;

COMMIT TRANSACTION;
