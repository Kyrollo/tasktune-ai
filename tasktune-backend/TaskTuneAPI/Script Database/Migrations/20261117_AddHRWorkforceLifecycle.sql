/*
  HR workforce lifecycle: recruitment, onboarding, goals, expenses, assets and benefits.
  Additive and safe to re-run. Back up and test on a restored database before production.
*/
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.JobApplicants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.JobApplicants (
        JobApplicantId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_JobApplicants PRIMARY KEY,
        CompanyId int NOT NULL,
        CandidateName nvarchar(160) NOT NULL,
        Email nvarchar(254) NOT NULL CONSTRAINT DF_JobApplicants_Email DEFAULT N'',
        Phone nvarchar(40) NOT NULL CONSTRAINT DF_JobApplicants_Phone DEFAULT N'',
        JobTitle nvarchar(160) NOT NULL,
        Source nvarchar(80) NOT NULL CONSTRAINT DF_JobApplicants_Source DEFAULT N'direct',
        Stage nvarchar(24) NOT NULL CONSTRAINT DF_JobApplicants_Stage DEFAULT N'applied',
        Notes nvarchar(2000) NOT NULL CONSTRAINT DF_JobApplicants_Notes DEFAULT N'',
        CreatedBy int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_JobApplicants_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_JobApplicants_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_JobApplicants_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_JobApplicants_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_JobApplicants_Stage CHECK (Stage IN (N'applied',N'screening',N'interview',N'offer',N'hired',N'rejected'))
    );
    CREATE INDEX IX_JobApplicants_Company_Stage ON dbo.JobApplicants(CompanyId, Stage, UpdatedAt DESC);
END;

IF OBJECT_ID(N'dbo.OnboardingTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OnboardingTasks (
        OnboardingTaskId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OnboardingTasks PRIMARY KEY,
        CompanyId int NOT NULL,
        EmployeeUserId int NOT NULL,
        Title nvarchar(180) NOT NULL,
        Description nvarchar(2000) NOT NULL CONSTRAINT DF_OnboardingTasks_Description DEFAULT N'',
        DueDate date NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_OnboardingTasks_Status DEFAULT N'open',
        CreatedBy int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_OnboardingTasks_CreatedAt DEFAULT SYSUTCDATETIME(),
        CompletedAt datetime2(3) NULL,
        CONSTRAINT FK_OnboardingTasks_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_OnboardingTasks_Employee FOREIGN KEY (EmployeeUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_OnboardingTasks_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_OnboardingTasks_Status CHECK (Status IN (N'open',N'completed'))
    );
    CREATE INDEX IX_OnboardingTasks_Company_Employee_Status ON dbo.OnboardingTasks(CompanyId, EmployeeUserId, Status, DueDate);
END;

IF OBJECT_ID(N'dbo.PerformanceGoals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PerformanceGoals (
        PerformanceGoalId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PerformanceGoals PRIMARY KEY,
        CompanyId int NOT NULL,
        UserId int NOT NULL,
        Title nvarchar(180) NOT NULL,
        Description nvarchar(2000) NOT NULL CONSTRAINT DF_PerformanceGoals_Description DEFAULT N'',
        MetricName nvarchar(100) NOT NULL CONSTRAINT DF_PerformanceGoals_Metric DEFAULT N'',
        TargetValue decimal(18,2) NOT NULL,
        CurrentValue decimal(18,2) NOT NULL CONSTRAINT DF_PerformanceGoals_Current DEFAULT 0,
        StartDate date NOT NULL,
        EndDate date NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_PerformanceGoals_Status DEFAULT N'active',
        CreatedBy int NOT NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_PerformanceGoals_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_PerformanceGoals_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PerformanceGoals_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_PerformanceGoals_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_PerformanceGoals_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_PerformanceGoals_Values CHECK (TargetValue > 0 AND CurrentValue >= 0),
        CONSTRAINT CK_PerformanceGoals_Dates CHECK (EndDate >= StartDate),
        CONSTRAINT CK_PerformanceGoals_Status CHECK (Status IN (N'active',N'paused',N'completed'))
    );
    CREATE INDEX IX_PerformanceGoals_Company_User_Status ON dbo.PerformanceGoals(CompanyId, UserId, Status, EndDate);
END;

IF OBJECT_ID(N'dbo.ExpenseClaims', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExpenseClaims (
        ExpenseClaimId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ExpenseClaims PRIMARY KEY,
        CompanyId int NOT NULL,
        UserId int NOT NULL,
        Category nvarchar(100) NOT NULL,
        Description nvarchar(1800) NOT NULL CONSTRAINT DF_ExpenseClaims_Description DEFAULT N'',
        Amount decimal(18,2) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        ExpenseDate date NOT NULL,
        ReceiptReference nvarchar(500) NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_ExpenseClaims_Status DEFAULT N'pending',
        ReviewComment nvarchar(1200) NULL,
        ReviewedBy int NULL,
        ReviewedAt datetime2(3) NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_ExpenseClaims_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ExpenseClaims_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_ExpenseClaims_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_ExpenseClaims_ReviewedBy FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_ExpenseClaims_Amount CHECK (Amount > 0),
        CONSTRAINT CK_ExpenseClaims_Status CHECK (Status IN (N'pending',N'approved',N'rejected'))
    );
    CREATE INDEX IX_ExpenseClaims_Company_User_Status ON dbo.ExpenseClaims(CompanyId, UserId, Status, CreatedAt DESC);
END;

IF OBJECT_ID(N'dbo.CompanyAssets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyAssets (
        CompanyAssetId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CompanyAssets PRIMARY KEY,
        CompanyId int NOT NULL,
        AssetTag nvarchar(80) NOT NULL,
        Name nvarchar(180) NOT NULL,
        Category nvarchar(100) NOT NULL CONSTRAINT DF_CompanyAssets_Category DEFAULT N'',
        SerialNumber nvarchar(160) NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_CompanyAssets_Status DEFAULT N'available',
        AssignedToUserId int NULL,
        AssignedAt datetime2(3) NULL,
        ReturnedAt datetime2(3) NULL,
        Notes nvarchar(1000) NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_CompanyAssets_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_CompanyAssets_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_CompanyAssets_AssignedTo FOREIGN KEY (AssignedToUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_CompanyAssets_Status CHECK (Status IN (N'available',N'assigned',N'retired'))
    );
    CREATE UNIQUE INDEX UQ_CompanyAssets_Company_Tag ON dbo.CompanyAssets(CompanyId, AssetTag);
    CREATE INDEX IX_CompanyAssets_Company_Assignee ON dbo.CompanyAssets(CompanyId, AssignedToUserId, Status);
END;

IF OBJECT_ID(N'dbo.BenefitPlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BenefitPlans (
        BenefitPlanId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BenefitPlans PRIMARY KEY,
        CompanyId int NOT NULL,
        Name nvarchar(180) NOT NULL,
        ProviderName nvarchar(180) NOT NULL CONSTRAINT DF_BenefitPlans_Provider DEFAULT N'',
        Description nvarchar(1200) NOT NULL CONSTRAINT DF_BenefitPlans_Description DEFAULT N'',
        EmployeeContribution decimal(18,2) NOT NULL CONSTRAINT DF_BenefitPlans_Employee DEFAULT 0,
        EmployerContribution decimal(18,2) NOT NULL CONSTRAINT DF_BenefitPlans_Employer DEFAULT 0,
        CurrencyCode char(3) NOT NULL,
        IsActive bit NOT NULL CONSTRAINT DF_BenefitPlans_IsActive DEFAULT 1,
        EffectiveFrom date NOT NULL,
        EffectiveTo date NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_BenefitPlans_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_BenefitPlans_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT CK_BenefitPlans_Contributions CHECK (EmployeeContribution >= 0 AND EmployerContribution >= 0),
        CONSTRAINT CK_BenefitPlans_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );
    CREATE INDEX IX_BenefitPlans_Company_Active ON dbo.BenefitPlans(CompanyId, IsActive, EffectiveFrom);
END;

IF OBJECT_ID(N'dbo.BenefitEnrollments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BenefitEnrollments (
        BenefitEnrollmentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_BenefitEnrollments PRIMARY KEY,
        CompanyId int NOT NULL,
        BenefitPlanId int NOT NULL,
        UserId int NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_BenefitEnrollments_Status DEFAULT N'pending',
        Notes nvarchar(1000) NULL,
        CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_BenefitEnrollments_CreatedAt DEFAULT SYSUTCDATETIME(),
        ReviewedAt datetime2(3) NULL,
        ReviewedBy int NULL,
        CONSTRAINT FK_BenefitEnrollments_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(CompanyId),
        CONSTRAINT FK_BenefitEnrollments_Plans FOREIGN KEY (BenefitPlanId) REFERENCES dbo.BenefitPlans(BenefitPlanId),
        CONSTRAINT FK_BenefitEnrollments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT FK_BenefitEnrollments_ReviewedBy FOREIGN KEY (ReviewedBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT CK_BenefitEnrollments_Status CHECK (Status IN (N'pending',N'active',N'declined'))
    );
    CREATE UNIQUE INDEX UQ_BenefitEnrollments_Company_Plan_User ON dbo.BenefitEnrollments(CompanyId, BenefitPlanId, UserId);
    CREATE INDEX IX_BenefitEnrollments_Company_User_Status ON dbo.BenefitEnrollments(CompanyId, UserId, Status);
END;

COMMIT TRANSACTION;
