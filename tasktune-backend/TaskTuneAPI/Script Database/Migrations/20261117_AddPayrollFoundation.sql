/*
  Country-neutral payroll records.
  Statutory deductions are stored as HR-entered amounts; this migration does not encode tax law.
  Apply only after a database backup and validation on a restored copy.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PayrollPeriods', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PayrollPeriods (
        PayrollPeriodId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayrollPeriods PRIMARY KEY,
        CompanyId int NOT NULL,
        PeriodStart date NOT NULL,
        PeriodEnd date NOT NULL,
        CurrencyCode char(3) NOT NULL CONSTRAINT DF_PayrollPeriods_Currency DEFAULT 'EGP',
        Status nvarchar(20) NOT NULL CONSTRAINT DF_PayrollPeriods_Status DEFAULT N'draft',
        CreatedBy int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_PayrollPeriods_CreatedAt DEFAULT SYSUTCDATETIME(),
        ApprovedAt datetime2(3) NULL,
        ApprovedBy int NULL,
        CONSTRAINT FK_PayrollPeriods_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_PayrollPeriods_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_PayrollPeriods_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_PayrollPeriods_Dates CHECK (PeriodEnd >= PeriodStart),
        CONSTRAINT CK_PayrollPeriods_Status CHECK (Status IN (N'draft',N'approved',N'paid'))
    );
    CREATE UNIQUE INDEX UQ_PayrollPeriods_Company_Period ON dbo.PayrollPeriods(CompanyId, PeriodStart, PeriodEnd);
END;

IF OBJECT_ID(N'dbo.Payslips', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payslips (
        PayslipId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payslips PRIMARY KEY,
        CompanyId int NOT NULL,
        PayrollPeriodId int NOT NULL,
        UserId int NOT NULL,
        BaseSalary decimal(18,2) NOT NULL,
        Allowances decimal(18,2) NOT NULL CONSTRAINT DF_Payslips_Allowances DEFAULT 0,
        Bonus decimal(18,2) NOT NULL CONSTRAINT DF_Payslips_Bonus DEFAULT 0,
        IncomeTaxDeduction decimal(18,2) NOT NULL CONSTRAINT DF_Payslips_IncomeTax DEFAULT 0,
        SocialInsuranceDeduction decimal(18,2) NOT NULL CONSTRAINT DF_Payslips_SocialInsurance DEFAULT 0,
        OtherDeductions decimal(18,2) NOT NULL CONSTRAINT DF_Payslips_OtherDeductions DEFAULT 0,
        CurrencyCode char(3) NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_Payslips_Status DEFAULT N'draft',
        Notes nvarchar(1500) NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_Payslips_CreatedAt DEFAULT SYSUTCDATETIME(),
        PaidAt datetime2(3) NULL,
        PaidBy int NULL,
        CONSTRAINT FK_Payslips_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_Payslips_Periods FOREIGN KEY (PayrollPeriodId) REFERENCES dbo.PayrollPeriods(PayrollPeriodId),
        CONSTRAINT FK_Payslips_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_Payslips_PaidBy FOREIGN KEY (PaidBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_Payslips_Amounts CHECK (
            BaseSalary >= 0 AND Allowances >= 0 AND Bonus >= 0 AND
            IncomeTaxDeduction >= 0 AND SocialInsuranceDeduction >= 0 AND OtherDeductions >= 0 AND
            BaseSalary + Allowances + Bonus >= IncomeTaxDeduction + SocialInsuranceDeduction + OtherDeductions
        ),
        CONSTRAINT CK_Payslips_Status CHECK (Status IN (N'draft',N'approved',N'paid'))
    );
    CREATE UNIQUE INDEX UQ_Payslips_Company_Period_User ON dbo.Payslips(CompanyId, PayrollPeriodId, UserId);
    CREATE INDEX IX_Payslips_Company_User_Created ON dbo.Payslips(CompanyId, UserId, CreatedAt DESC);
END;

COMMIT TRANSACTION;
