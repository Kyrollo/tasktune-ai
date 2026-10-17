USE [TaskTuneAIDatabase]
GO

SET NOCOUNT ON;

DECLARE @CompanyId INT;
SELECT @CompanyId = CompanyId FROM [dbo].[Companies] WHERE CompanyName = 'TaskTune Test Company';

IF @CompanyId IS NULL
BEGIN
    PRINT 'Test company not found.';
    RETURN;
END

-- Variables for our specific seeded users
DECLARE @CeoId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'ceo@tasktune.ai');
DECLARE @ManagerId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'manager@tasktune.ai');
DECLARE @SupervisorId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'supervisor@tasktune.ai');
DECLARE @HrId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'hr@tasktune.ai');
DECLARE @Emp1Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee@tasktune.ai');
DECLARE @Emp2Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee2@tasktune.ai');
DECLARE @Emp3Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee3@tasktune.ai');

-- 1. Insert Departments
PRINT 'Inserting Departments...';
INSERT INTO [dbo].[Departments] (CompanyId, DepartmentName, Description, CreatedAt, UpdatedAt)
VALUES
(@CompanyId, 'Engineering', 'Core technical and software development department.', GETDATE(), GETDATE()),
(@CompanyId, 'Marketing & Design', 'Creative marketing and growth.', GETDATE(), GETDATE()),
(@CompanyId, 'Human Resources', 'People and culture operations.', GETDATE(), GETDATE());

DECLARE @DeptEng INT = (SELECT TOP 1 DepartmentId FROM [dbo].[Departments] WHERE DepartmentName = 'Engineering' AND CompanyId = @CompanyId ORDER BY DepartmentId DESC);
DECLARE @DeptMkt INT = (SELECT TOP 1 DepartmentId FROM [dbo].[Departments] WHERE DepartmentName = 'Marketing & Design' AND CompanyId = @CompanyId ORDER BY DepartmentId DESC);
DECLARE @DeptHR INT = (SELECT TOP 1 DepartmentId FROM [dbo].[Departments] WHERE DepartmentName = 'Human Resources' AND CompanyId = @CompanyId ORDER BY DepartmentId DESC);

-- 2. Map Users to Departments
PRINT 'Mapping Users to Departments...';
UPDATE [dbo].[Users] SET DepartmentId = @DeptEng WHERE UserId IN (@ManagerId, @Emp1Id, @Emp2Id);
UPDATE [dbo].[Users] SET DepartmentId = @DeptMkt WHERE UserId IN (@SupervisorId, @Emp3Id);
UPDATE [dbo].[Users] SET DepartmentId = @DeptHR WHERE UserId IN (@HrId);

-- 3. Insert Projects
PRINT 'Inserting Projects...';
INSERT INTO [dbo].[Projects] (CompanyId, ProjectName, Description, StartDate, EndDate, Status, ManagerId, CreatedAt, UpdatedAt)
VALUES
(@CompanyId, 'TaskTune V2 Migration', 'Migrate all legacy systems to the new React and .NET 8 architecture.', GETDATE(), DATEADD(month, 3, GETDATE()), 1, @ManagerId, GETDATE(), GETDATE()),
(@CompanyId, 'Q2 Marketing Campaign', 'Launch new ad creatives across social platforms to boost user acquisition.', GETDATE(), DATEADD(month, 1, GETDATE()), 1, @SupervisorId, GETDATE(), GETDATE()),
(@CompanyId, 'Employee Onboarding Revamp', 'Redesign the internal onboarding documentation for new hires.', DATEADD(day, -10, GETDATE()), DATEADD(month, 2, GETDATE()), 0, @HrId, GETDATE(), GETDATE());

DECLARE @ProjV2 INT = (SELECT TOP 1 ProjectId FROM [dbo].[Projects] WHERE ProjectName = 'TaskTune V2 Migration' AND CompanyId = @CompanyId ORDER BY ProjectId DESC);
DECLARE @ProjMkt INT = (SELECT TOP 1 ProjectId FROM [dbo].[Projects] WHERE ProjectName = 'Q2 Marketing Campaign' AND CompanyId = @CompanyId ORDER BY ProjectId DESC);
DECLARE @ProjHR INT = (SELECT TOP 1 ProjectId FROM [dbo].[Projects] WHERE ProjectName = 'Employee Onboarding Revamp' AND CompanyId = @CompanyId ORDER BY ProjectId DESC);

-- 4. Insert Tasks
PRINT 'Inserting Tasks...';
-- Note: CK_Tasks_Dates constraint requires DueDate >= StartDate
INSERT INTO [dbo].[Tasks] (ProjectId, TaskName, Description, AssignedTo, Priority, Status, StartDate, DueDate, CreatedBy, CreatedAt, UpdatedAt)
VALUES
-- Eng Project (StartDate is today, DueDate is in future)
(@ProjV2, 'Setup AWS Infrastructure', 'Provision EC2 and RDS instances for the production environment.', @Emp1Id, 2, 2, GETDATE(), DATEADD(day, 2, GETDATE()), @ManagerId, GETDATE(), GETDATE()),
(@ProjV2, 'Develop Core API Authentication', 'Implement the strictly hashed BCrypt auth endpoints.', @Emp1Id, 2, 1, GETDATE(), DATEADD(day, 4, GETDATE()), @ManagerId, GETDATE(), GETDATE()),
(@ProjV2, 'Frontend React Dashboard', 'Port old MVC views to the new React + Vite Dashboard.', @Emp2Id, 1, 1, GETDATE(), DATEADD(day, 10, GETDATE()), @ManagerId, GETDATE(), GETDATE()),
(@ProjV2, 'Database Migration Scripts', 'Write SQL scripts to safely transfer data over.', @ManagerId, 1, 0, GETDATE(), DATEADD(day, 15, GETDATE()), @ManagerId, GETDATE(), GETDATE()),

-- Marketing Project
(@ProjMkt, 'Design Social Banner Variations', 'Create 5 banner variations for LinkedIn.', @Emp3Id, 1, 1, GETDATE(), DATEADD(day, 5, GETDATE()), @SupervisorId, GETDATE(), GETDATE()),
(@ProjMkt, 'Draft Ad Copywriting', 'Write 3 variants of ad copy to pair with the banners.', @SupervisorId, 2, 2, GETDATE(), DATEADD(day, 1, GETDATE()), @SupervisorId, GETDATE(), GETDATE()),

-- HR Project
(@ProjHR, 'Update Welcome Guide', 'Rewrite the internal employee handbook PDF.', @HrId, 0, 0, GETDATE(), DATEADD(day, 20, GETDATE()), @HrId, GETDATE(), GETDATE());

-- 5. Insert Subtasks for some Tasks
PRINT 'Inserting Subtasks...';
DECLARE @TaskAws INT = (SELECT TOP 1 TaskId FROM [dbo].[Tasks] WHERE TaskName = 'Setup AWS Infrastructure' AND ProjectId = @ProjV2 ORDER BY TaskId DESC);
DECLARE @TaskApi INT = (SELECT TOP 1 TaskId FROM [dbo].[Tasks] WHERE TaskName = 'Develop Core API Authentication' AND ProjectId = @ProjV2 ORDER BY TaskId DESC);

INSERT INTO [dbo].[Subtasks] (TaskId, SubtaskName, Status, CreatedAt, UpdatedAt)
VALUES
(@TaskAws, 'Provision VPC', 2, GETDATE(), GETDATE()),
(@TaskAws, 'Setup Security Groups', 2, GETDATE(), GETDATE()),
(@TaskAws, 'Deploy RDS Database', 2, GETDATE(), GETDATE()),
(@TaskApi, 'Create Token Generator', 2, GETDATE(), GETDATE()),
(@TaskApi, 'Apply Role Permissions Middleware', 0, GETDATE(), GETDATE());

-- 6. Insert Role Permissions for Company 6 (Mirrored from Company 1)
PRINT 'Inserting Role Permissions...';
IF NOT EXISTS (SELECT 1 FROM [dbo].[RolePermissions] WHERE CompanyId = @CompanyId)
BEGIN
    INSERT INTO [dbo].[RolePermissions] (CompanyId, Role, Resource, Action, CanPerform, CreatedAt)
    SELECT @CompanyId, Role, Resource, Action, CanPerform, GETDATE()
    FROM [dbo].[RolePermissions]
    WHERE CompanyId = 1;
END

PRINT '==================================================';
PRINT 'MOCK DATA SUCCESSFULLY INJECTED FOR TASKTUNE TEST COMPANY!';
PRINT '==================================================';
GO