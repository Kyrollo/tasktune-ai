DECLARE @Comp INT = 6;
DECLARE @Manager INT = (SELECT TOP 1 UserId FROM Users WHERE Email = 'manager@tasktune.ai');
DECLARE @Emp1 INT = (SELECT TOP 1 UserId FROM Users WHERE Email = 'employee@tasktune.ai');
DECLARE @Emp2 INT = (SELECT TOP 1 UserId FROM Users WHERE Email = 'employee2@tasktune.ai');
DECLARE @Task1 INT = (SELECT TOP 1 TaskId FROM Tasks WHERE ProjectId IN (SELECT ProjectId FROM Projects WHERE CompanyId = @Comp));

INSERT INTO AuditLogs (UserId, Action, ResourceType, ResourceId, ChangesJson, IpAddress, UserAgent, Timestamp) VALUES
(@Manager, 'UpdateProject', 'Project', 1, '{"status": "active"}', '192.168.1.1', 'Mozilla/5.0', DATEADD(d, -1, GETDATE())),
(@Manager, 'CreateTask', 'Task', @Task1, '{"taskName": "Initial setup"}', '192.168.1.5', 'Mozilla/5.0', DATEADD(hour, -5, GETDATE())),
(@Emp1, 'CompleteTask', 'Task', @Task1, '{"status": "completed"}', '192.168.1.10', 'Chrome/100', DATEADD(hour, -2, GETDATE())),
(@Emp2, 'Login', 'UserSession', @Emp2, '{"loggedIn": true}', '192.168.1.20', 'Safari/537', DATEADD(minute, -15, GETDATE())),
(@Manager, 'ApproveEvaluation', 'Evaluation', 1, '{"score": 95}', '192.168.1.1', 'Mozilla/5.0', DATEADD(minute, -10, GETDATE()));
