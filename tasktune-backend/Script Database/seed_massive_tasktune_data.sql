USE [TaskTuneAIDatabase];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

PRINT 'Starting Massive Data Injection for TaskTune (CompanyId = 6)...';

DECLARE @CompanyId INT = 6;
DECLARE @AdminId INT = (SELECT UserId FROM Users WHERE Email = 'admin@tasktune.ai');

-- Ensure Departments
DECLARE @DeptEng INT = (SELECT TOP 1 DepartmentId FROM Departments WHERE DepartmentName = 'Engineering' AND CompanyId = @CompanyId);
DECLARE @DeptMkt INT = (SELECT TOP 1 DepartmentId FROM Departments WHERE DepartmentName = 'Marketing & Design' AND CompanyId = @CompanyId);
DECLARE @DeptHR INT = (SELECT TOP 1 DepartmentId FROM Departments WHERE DepartmentName = 'Human Resources' AND CompanyId = @CompanyId);

IF @DeptEng IS NULL OR @DeptMkt IS NULL OR @DeptHR IS NULL
BEGIN
    PRINT 'ERROR: Core departments not found. Run previous mock data script first.'
    RETURN;
END

-- Ensure Positions
DECLARE @PosEng INT = (SELECT TOP 1 PositionId FROM Positions WHERE PositionTitle = 'Senior Developer' AND CompanyId = @CompanyId);
DECLARE @PosMkt INT = (SELECT TOP 1 PositionId FROM Positions WHERE PositionTitle = 'Lead Designer' AND CompanyId = @CompanyId);

-- GENERATE 30 USERS
PRINT 'Generating 30 Users...';
DECLARE @UserCounter INT = 1;
WHILE @UserCounter <= 30
BEGIN
    DECLARE @FirstName NVARCHAR(50) = CHAR(65 + (@UserCounter % 26)) + 'Employee' + CAST(@UserCounter AS NVARCHAR);
    DECLARE @LastName NVARCHAR(50) = 'Smith' + CAST(@UserCounter AS NVARCHAR);
    DECLARE @Email NVARCHAR(100) = 'sim_user' + CAST(@UserCounter AS NVARCHAR) + '@tasktune.ai';
    DECLARE @DeptId INT = IIF(@UserCounter % 2 = 0, @DeptEng, @DeptMkt);
    DECLARE @PosId INT = IIF(@UserCounter % 2 = 0, @PosEng, @PosMkt);
    
    IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = @Email)
    BEGIN
        INSERT INTO Users (CompanyId, Email, PasswordHash, FirstName, LastName, Role, IsActive, DepartmentId, PositionId, CreatedAt, UpdatedAt)
        VALUES (
            @CompanyId, @Email, 
            '$2a$11$w.3K.C.Q5e1O3.jK0sUxu.zZpZXZxU.nJ6w7lXYq.UvjQO/vjV.H2', -- Password123!
            @FirstName, @LastName, 0, 1, @DeptId, @PosId, 
            DATEADD(day, -(@UserCounter * 5), GETDATE()), GETDATE()
        );
    END
    SET @UserCounter = @UserCounter + 1;
END

-- GENERATE 15 PROJECTS
PRINT 'Generating 15 Projects...';
DECLARE @ProjCounter INT = 1;
DECLARE @ManagerId INT = (SELECT TOP 1 UserId FROM Users WHERE Email = 'manager@tasktune.ai' AND CompanyId = @CompanyId);

WHILE @ProjCounter <= 15
BEGIN
    DECLARE @ProjName NVARCHAR(100) = 'Simulated Project Alpha ' + CAST(@ProjCounter AS NVARCHAR);
    DECLARE @Status INT = IIF(@ProjCounter % 3 = 0, 2, IIF(@ProjCounter % 2 = 0, 1, 0)); -- 0: Pending, 1: InProgress, 2: Completed
    
    IF NOT EXISTS (SELECT 1 FROM Projects WHERE ProjectName = @ProjName AND CompanyId = @CompanyId)
    BEGIN
        INSERT INTO Projects (CompanyId, ProjectName, StartDate, EndDate, Status, ManagerId, CreatedAt, UpdatedAt)
        VALUES (
            @CompanyId, @ProjName, 
            DATEADD(day, -(@ProjCounter * 10), GETDATE()), 
            DATEADD(day, (@ProjCounter * 5), GETDATE()), 
            @Status, @ManagerId, GETDATE(), GETDATE()
        );
    END
    SET @ProjCounter = @ProjCounter + 1;
END

-- GENERATE 100 TASKS ACROSS PROJECTS
PRINT 'Generating 100 Tasks...';
DECLARE @TaskCounter INT = 1;
-- Get all simulation users
DECLARE @MinUserId INT = (SELECT MIN(UserId) FROM Users WHERE Email LIKE 'sim_user%');
DECLARE @MaxUserId INT = (SELECT MAX(UserId) FROM Users WHERE Email LIKE 'sim_user%');

WHILE @TaskCounter <= 100
BEGIN
    DECLARE @RandProj INT = (SELECT TOP 1 ProjectId FROM Projects WHERE CompanyId = @CompanyId ORDER BY NEWID());
    DECLARE @RandUser INT;
    SELECT TOP 1 @RandUser = UserId FROM Users WHERE Email LIKE 'sim_user%' ORDER BY NEWID();
    
    DECLARE @TaskName NVARCHAR(200) = 'Auto-Generated Task ' + CAST(@TaskCounter AS NVARCHAR);
    DECLARE @TStatus INT = IIF(@TaskCounter % 4 = 0, 2, IIF(@TaskCounter % 2 = 0, 1, 0)); 
    DECLARE @Prio INT = @TaskCounter % 3; -- 0: Low, 1: Medium, 2: High
    
    INSERT INTO Tasks (ProjectId, TaskName, AssignedTo, Priority, Status, StartDate, DueDate, CreatedBy, CreatedAt, UpdatedAt, CompletionPercentage)
    VALUES (
        @RandProj, @TaskName, @RandUser, @Prio, @TStatus, 
        DATEADD(day, -(@TaskCounter % 20), GETDATE()), 
        DATEADD(day, (@TaskCounter % 15) + 1, GETDATE()), 
        @ManagerId, GETDATE(), GETDATE(), 
        IIF(@TStatus = 2, 100, IIF(@TStatus = 1, 50, 0))
    );
    
    SET @TaskCounter = @TaskCounter + 1;
END

-- GENERATE 50 EVALUATIONS
PRINT 'Generating 50 Evaluations...';
DECLARE @EvalCounter INT = 1;

WHILE @EvalCounter <= 50
BEGIN
    DECLARE @ETaskId INT;
    SELECT TOP 1 @ETaskId = TaskId FROM Tasks WHERE TaskName LIKE 'Auto-Generated Task %' ORDER BY NEWID();
    
    DECLARE @EEmpId INT;
    SELECT @EEmpId = AssignedTo FROM Tasks WHERE TaskId = @ETaskId;
    
    IF @ETaskId IS NOT NULL AND @EEmpId IS NOT NULL
    BEGIN
        DECLARE @BaseScore INT = 70 + (@EvalCounter % 30); -- Score between 70 and 99
        
        INSERT INTO Evaluations (TaskId, EvaluatedUserId, EvaluatorId, OverallScore, QualityScore, TimelinessScore, CollaborationScore, ManualFeedback, AiGeneratedFeedback, Status, CreatedAt)
        VALUES (
            @ETaskId, @EEmpId, @ManagerId, 
            @BaseScore, @BaseScore, @BaseScore - 2, @BaseScore + 1, 
            'Sturdy effort on ' + CAST(@ETaskId AS NVARCHAR), 
            'AI says performance is adequate.', 1, GETDATE()
        );
    END
    
    SET @EvalCounter = @EvalCounter + 1;
END

-- GENERATE 500 AUDIT LOGS OVER LAST 6 MONTHS
PRINT 'Generating 500+ Audit Logs...';
DECLARE @LogCounter INT = 1;
WHILE @LogCounter <= 500
BEGIN
    DECLARE @LogUserId INT;
    SELECT TOP 1 @LogUserId = UserId FROM Users WHERE CompanyId = @CompanyId ORDER BY NEWID();
    
    DECLARE @ActionType NVARCHAR(50) = IIF(@LogCounter % 5 = 0, 'UPDATE', IIF(@LogCounter % 3 = 0, 'DELETE', 'CREATE'));
    DECLARE @ResType NVARCHAR(50) = IIF(@LogCounter % 4 = 0, 'Task', IIF(@LogCounter % 2 = 0, 'User', 'Project'));
    DECLARE @TimeOffset INT = -(@LogCounter % 180); -- Up to 180 days back
    
    INSERT INTO AuditLogs (UserId, Action, ResourceType, ResourceId, ChangesJson, IpAddress, Timestamp)
    VALUES (
        @LogUserId, @ActionType, @ResType, CAST((@LogCounter % 100) AS NVARCHAR),
        '{"field": "status", "old": "pending", "new": "active"}',
        '192.168.1.' + CAST((@LogCounter % 255) AS NVARCHAR),
        DATEADD(day, @TimeOffset, GETDATE())
    );
    
    SET @LogCounter = @LogCounter + 1;
END

PRINT '==================================================';
PRINT 'MASSIVE DATA INJECTION COMPLETELY FINISHED!';
PRINT '==================================================';
GO