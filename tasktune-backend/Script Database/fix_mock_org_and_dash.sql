USE [TaskTuneAIDatabase];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

PRINT 'Cleaning up multiple bad mock inserts...';
DECLARE @CompanyId INT = 6;

-- Delete all users' ties to position and dept
UPDATE Users SET DepartmentId = NULL, PositionId = NULL WHERE CompanyId = @CompanyId;

-- Delete all subtasks for this company's tasks
DELETE FROM Subtasks WHERE TaskId IN (SELECT TaskId FROM Tasks t JOIN Projects p ON t.ProjectId = p.ProjectId WHERE p.CompanyId = @CompanyId);

-- Delete Evaluations
DELETE FROM Evaluations WHERE EvaluatedUserId IN (SELECT UserId FROM Users WHERE CompanyId = @CompanyId);

-- Delete Tasks
DELETE FROM Tasks WHERE ProjectId IN (SELECT ProjectId FROM Projects WHERE CompanyId = @CompanyId);

-- Delete Projects
DELETE FROM Projects WHERE CompanyId = @CompanyId;

-- Delete Positions
DELETE FROM Positions WHERE CompanyId = @CompanyId;

-- Delete Departments
DELETE FROM Departments WHERE CompanyId = @CompanyId;


PRINT 'Inserting fresh pristine data for Company 6...';
DECLARE @ManagerId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'manager@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @SupervisorId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'supervisor@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @Emp1Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @Emp2Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee2@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @Emp3Id INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'employee3@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @HrId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'hr@tasktune.ai' AND CompanyId = @CompanyId);
DECLARE @CeoId INT = (SELECT UserId FROM [dbo].[Users] WHERE Email = 'ceo@tasktune.ai' AND CompanyId = @CompanyId);

-- Add Departments
INSERT INTO Departments (CompanyId, DepartmentName, Description, CreatedAt, UpdatedAt) VALUES
(@CompanyId, 'Executive Management', 'Company Executives', GETDATE(), GETDATE()),
(@CompanyId, 'Engineering', 'Core engineering', GETDATE(), GETDATE()),
(@CompanyId, 'Marketing & Design', 'Marketing efforts', GETDATE(), GETDATE()),
(@CompanyId, 'Human Resources', 'HR & Culture', GETDATE(), GETDATE());

DECLARE @DeptExec INT = (SELECT DepartmentId FROM Departments WHERE DepartmentName = 'Executive Management' AND CompanyId = @CompanyId);
DECLARE @DeptEng INT = (SELECT DepartmentId FROM Departments WHERE DepartmentName = 'Engineering' AND CompanyId = @CompanyId);
DECLARE @DeptMkt INT = (SELECT DepartmentId FROM Departments WHERE DepartmentName = 'Marketing & Design' AND CompanyId = @CompanyId);
DECLARE @DeptHR INT = (SELECT DepartmentId FROM Departments WHERE DepartmentName = 'Human Resources' AND CompanyId = @CompanyId);

-- Add Positions
INSERT INTO Positions (CompanyId, DepartmentId, PositionTitle, HierarchyLevel, CreatedAt, UpdatedAt) VALUES 
(@CompanyId, @DeptExec, 'Chief Executive Officer', 1, GETDATE(), GETDATE()),
(@CompanyId, @DeptEng, 'Engineering Manager', 2, GETDATE(), GETDATE()),
(@CompanyId, @DeptMkt, 'Marketing Supervisor', 2, GETDATE(), GETDATE()),
(@CompanyId, @DeptEng, 'Senior Developer', 3, GETDATE(), GETDATE()),
(@CompanyId, @DeptMkt, 'Lead Designer', 3, GETDATE(), GETDATE()),
(@CompanyId, @DeptHR, 'HR Director', 2, GETDATE(), GETDATE());

DECLARE @PosCeo INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'Chief Executive Officer' AND CompanyId = @CompanyId);
DECLARE @PosEng1 INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'Engineering Manager' AND CompanyId = @CompanyId);
DECLARE @PosEng2 INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'Senior Developer' AND CompanyId = @CompanyId);
DECLARE @PosMkt1 INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'Marketing Supervisor' AND CompanyId = @CompanyId);
DECLARE @PosMkt2 INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'Lead Designer' AND CompanyId = @CompanyId);
DECLARE @PosHr1 INT = (SELECT PositionId FROM Positions WHERE PositionTitle = 'HR Director' AND CompanyId = @CompanyId);

-- Map Users
UPDATE Users SET DepartmentId = @DeptEng, PositionId = @PosEng1 WHERE UserId = @ManagerId;
UPDATE Users SET DepartmentId = @DeptEng, PositionId = @PosEng2 WHERE UserId IN (@Emp1Id, @Emp2Id);
UPDATE Users SET DepartmentId = @DeptMkt, PositionId = @PosMkt1 WHERE UserId = @SupervisorId;
UPDATE Users SET DepartmentId = @DeptMkt, PositionId = @PosMkt2 WHERE UserId = @Emp3Id;
UPDATE Users SET DepartmentId = @DeptHR, PositionId = @PosHr1 WHERE UserId = @HrId;
UPDATE Users SET DepartmentId = @DeptExec, PositionId = @PosCeo WHERE UserId = @CeoId;

PRINT 'Mapping projects and evaluations...';
-- Projects
INSERT INTO Projects (CompanyId, ProjectName, StartDate, EndDate, Status, ManagerId, CreatedAt, UpdatedAt) VALUES
(@CompanyId, 'Platform V2', DATEADD(m, -1, GETDATE()), DATEADD(m, 2, GETDATE()), 1, @ManagerId, GETDATE(), GETDATE()),
(@CompanyId, 'Ad Campaign', DATEADD(m, -1, GETDATE()), DATEADD(m, 1, GETDATE()), 2, @SupervisorId, GETDATE(), GETDATE());

DECLARE @Proj1 INT = (SELECT ProjectId FROM Projects WHERE ProjectName = 'Platform V2' AND CompanyId = @CompanyId);
DECLARE @Proj2 INT = (SELECT ProjectId FROM Projects WHERE ProjectName = 'Ad Campaign' AND CompanyId = @CompanyId);

-- Tasks
INSERT INTO Tasks (ProjectId, TaskName, AssignedTo, Priority, Status, StartDate, DueDate, CreatedBy, CreatedAt, UpdatedAt, CompletionPercentage) VALUES
(@Proj1, 'DB Migration', @Emp1Id, 2, 2, DATEADD(d, -10, GETDATE()), DATEADD(d, -5, GETDATE()), @ManagerId, GETDATE(), GETDATE(), 100),
(@Proj1, 'UI Revamp', @Emp2Id, 1, 1, DATEADD(d, -5, GETDATE()), DATEADD(d, 5, GETDATE()), @ManagerId, GETDATE(), GETDATE(), 50),
(@Proj2, 'Ad Banners', @Emp3Id, 1, 2, DATEADD(d, -15, GETDATE()), DATEADD(d, -10, GETDATE()), @SupervisorId, GETDATE(), GETDATE(), 100);

-- Evaluations
DECLARE @Task1 INT = (SELECT TaskId FROM Tasks WHERE TaskName = 'DB Migration' AND ProjectId = @Proj1);
DECLARE @Task3 INT = (SELECT TaskId FROM Tasks WHERE TaskName = 'Ad Banners' AND ProjectId = @Proj2);

INSERT INTO Evaluations (TaskId, EvaluatedUserId, EvaluatorId, OverallScore, QualityScore, TimelinessScore, CollaborationScore, ManualFeedback, AiGeneratedFeedback, Status, CreatedAt) VALUES
(@Task1, @Emp1Id, @ManagerId, 95, 96, 94, 95, 'Great work', 'Excellent performance based on AI metrics.', 1, GETDATE()),
(@Task3, @Emp3Id, @SupervisorId, 88, 88, 85, 91, 'Good design', 'Good speed, needs slight polish.', 1, GETDATE()),
(@Task1, @ManagerId, @CeoId, 92, 92, 92, 92, 'Leadership is solid', 'Delegation could be improved.', 1, GETDATE());
GO