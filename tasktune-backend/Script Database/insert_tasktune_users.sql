-- Insert @tasktune.ai test users
USE [TaskTuneAIDatabase]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- First, insert a test company
IF NOT EXISTS (SELECT 1 FROM Companies WHERE CompanyName = 'TaskTune Test Company')
BEGIN
    INSERT INTO [dbo].[Companies] (CompanyName, CreatedAt, UpdatedAt)
    VALUES ('TaskTune Test Company', GETDATE(), GETDATE())
END
GO

DECLARE @CompanyId INT = (SELECT CompanyId FROM Companies WHERE CompanyName = 'TaskTune Test Company')

-- Insert test users
INSERT INTO [dbo].[Users] (
    [CompanyId], [Email], [PasswordHash], [FirstName], [LastName],
    [Phone], [Role], [IsActive], [CreatedAt], [UpdatedAt]
)
VALUES
    (@CompanyId, 'employee@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Employee', 'User', '123-456-7890', 0, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'employee2@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Employee', 'Two', '123-456-7891', 0, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'employee3@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Employee', 'Three', '123-456-7892', 0, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'employee4@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Employee', 'Four', '123-456-7893', 0, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'employee5@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Employee', 'Five', '123-456-7894', 0, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'supervisor@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Supervisor', 'User', '123-456-7895', 1, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'manager@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Manager', 'User', '123-456-7896', 2, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'hr@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'HR', 'User', '123-456-7897', 3, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'ceo@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'CEO', 'User', '123-456-7898', 4, 1, GETDATE(), GETDATE()),
    (@CompanyId, 'admin@tasktune.ai', '$2a$11$NVpiIRoUfx58XgBb3blcxuxxuEQobltw6woSLUpNOfjhnA.A27gbS', 'Admin', 'User', '123-456-7899', 5, 1, GETDATE(), GETDATE())

SELECT 'Test users inserted successfully' AS Result
GO
