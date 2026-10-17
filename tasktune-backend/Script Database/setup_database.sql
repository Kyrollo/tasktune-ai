USE [master]
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'TaskTuneAIDatabase')
BEGIN
    ALTER DATABASE [TaskTuneAIDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TaskTuneAIDatabase];
END
GO

CREATE DATABASE [TaskTuneAIDatabase]
GO

ALTER DATABASE [TaskTuneAIDatabase] SET RECOVERY SIMPLE
GO
ALTER DATABASE [TaskTuneAIDatabase] SET AUTO_CLOSE OFF
GO

USE [TaskTuneAIDatabase]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Companies](
    [CompanyId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyName] [nvarchar](255) NOT NULL,
    [IndustryPrimary] [nvarchar](max) NULL,
    [IndustrySecondary] [nvarchar](max) NULL,
    [SettingsJson] [nvarchar](max) NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Companies] PRIMARY KEY CLUSTERED ([CompanyId] ASC)
)
GO

CREATE TABLE [dbo].[Departments](
    [DepartmentId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [DepartmentName] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Departments] PRIMARY KEY CLUSTERED ([DepartmentId] ASC)
)
GO

CREATE TABLE [dbo].[Positions](
    [PositionId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [DepartmentId] [int] NULL,
    [PositionTitle] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [ReportsTo] [int] NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Positions] PRIMARY KEY CLUSTERED ([PositionId] ASC)
)
GO

CREATE TABLE [dbo].[Users](
    [UserId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [DepartmentId] [int] NULL,
    [PositionId] [int] NULL,
    [Email] [nvarchar](255) NOT NULL,
    [PasswordHash] [nvarchar](255) NOT NULL,
    [FirstName] [nvarchar](100) NOT NULL,
    [LastName] [nvarchar](100) NOT NULL,
    [FullName] AS (([FirstName]+N' ')+[LastName]) PERSISTED,
    [Phone] [nvarchar](20) NULL,
    [Role] [tinyint] NOT NULL,
    [IsActive] [bit] NOT NULL,
    [LastLogin] [datetime2](7) NULL,
    [RefreshToken] [nvarchar](500) NULL,
    [RefreshTokenExpiry] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_Users_Email] ON [dbo].[Users]([Email] ASC)
GO

CREATE TABLE [dbo].[Projects](
    [ProjectId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [ProjectName] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [ManagerId] [int] NOT NULL,
    [Status] [tinyint] NOT NULL,
    [StartDate] [date] NOT NULL,
    [EndDate] [date] NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Projects] PRIMARY KEY CLUSTERED ([ProjectId] ASC)
)
GO

CREATE TABLE [dbo].[Tasks](
    [TaskId] [int] IDENTITY(1,1) NOT NULL,
    [ProjectId] [int] NOT NULL,
    [TaskName] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [AssignedTo] [int] NOT NULL,
    [CreatedBy] [int] NOT NULL,
    [Status] [tinyint] NOT NULL CONSTRAINT [DF_Tasks_Status] DEFAULT (0),
    [Priority] [tinyint] NOT NULL CONSTRAINT [DF_Tasks_Priority] DEFAULT (1),
    [CompletionPercentage] [int] NOT NULL CONSTRAINT [DF_Tasks_CompletionPercentage] DEFAULT (0),
    [StartDate] [date] NULL,
    [DueDate] [date] NULL,
    [SubmitMethod] [nvarchar](255) NULL,
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Tasks_CreatedAt] DEFAULT (getutcdate()),
    [UpdatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Tasks_UpdatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_Tasks] PRIMARY KEY CLUSTERED ([TaskId] ASC)
)
GO

CREATE TABLE [dbo].[Subtasks](
    [SubtaskId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [SubtaskName] [nvarchar](255) NOT NULL,
    [Description] [nvarchar](max) NULL,
    [Status] [tinyint] NOT NULL CONSTRAINT [DF_Subtasks_Status] DEFAULT (0),
    [OrderIndex] [int] NOT NULL CONSTRAINT [DF_Subtasks_OrderIndex] DEFAULT (0),
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Subtasks_CreatedAt] DEFAULT (getutcdate()),
    [UpdatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Subtasks_UpdatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_Subtasks] PRIMARY KEY CLUSTERED ([SubtaskId] ASC)
)
GO

CREATE TABLE [dbo].[TaskProgress](
    [ProgressId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [SubmittedBy] [int] NOT NULL,
    [CompletionPercentage] [int] NOT NULL,
    [Notes] [nvarchar](max) NULL,
    [Blockers] [nvarchar](max) NULL,
    [SubmittedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_TaskProgress_SubmittedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_TaskProgress] PRIMARY KEY CLUSTERED ([ProgressId] ASC)
)
GO

CREATE TABLE [dbo].[TaskAttachments](
    [AttachmentId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [ProgressId] [int] NULL,
    [UploadedBy] [int] NOT NULL,
    [FileName] [nvarchar](255) NOT NULL,
    [FilePath] [nvarchar](500) NOT NULL,
    [MimeType] [nvarchar](100) NOT NULL,
    [FileSize] [bigint] NULL,
    [UploadedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_TaskAttachments_UploadedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_TaskAttachments] PRIMARY KEY CLUSTERED ([AttachmentId] ASC)
)
GO

CREATE TABLE [dbo].[TaskAccessControl](
    [AccessId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [UserId] [int] NOT NULL,
    [CanView] [bit] NOT NULL CONSTRAINT [DF_TaskAccessControl_CanView] DEFAULT (1),
    [CanEdit] [bit] NOT NULL CONSTRAINT [DF_TaskAccessControl_CanEdit] DEFAULT (0),
    [CanViewEvaluation] [bit] NOT NULL CONSTRAINT [DF_TaskAccessControl_CanViewEvaluation] DEFAULT (0),
    [CanEditEvaluation] [bit] NOT NULL CONSTRAINT [DF_TaskAccessControl_CanEditEvaluation] DEFAULT (0),
    [GrantedBy] [int] NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_TaskAccessControl_CreatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_TaskAccessControl] PRIMARY KEY CLUSTERED ([AccessId] ASC)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_TaskAccessControl_Task_User] ON [dbo].[TaskAccessControl]([TaskId] ASC, [UserId] ASC)
GO

CREATE TABLE [dbo].[TaskDependencies](
    [DependencyId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [DependsOnTaskId] [int] NOT NULL,
    [DependencyType] [tinyint] NOT NULL CONSTRAINT [DF_TaskDependencies_DependencyType] DEFAULT (0),
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_TaskDependencies_CreatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_TaskDependencies] PRIMARY KEY CLUSTERED ([DependencyId] ASC)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_TaskDependencies] ON [dbo].[TaskDependencies]([TaskId] ASC, [DependsOnTaskId] ASC)
GO

CREATE TABLE [dbo].[Evaluations](
    [EvaluationId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [EvaluatedUserId] [int] NOT NULL,
    [EvaluatorId] [int] NOT NULL,
    [OverallScore] [int] NOT NULL,
    [QualityScore] [int] NULL,
    [TimelinessScore] [int] NULL,
    [CollaborationScore] [int] NULL,
    [RiskLevel] [tinyint] NULL,
    [ManualFeedback] [nvarchar](max) NULL,
    [AiGeneratedFeedback] [nvarchar](max) NULL,
    [Status] [tinyint] NOT NULL,
    [SubmittedAt] [datetime2](7) NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_Evaluations] PRIMARY KEY CLUSTERED ([EvaluationId] ASC)
)
GO

CREATE TABLE [dbo].[Bugs](
    [BugId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [ReportedBy] [int] NOT NULL,
    [BugDescription] [nvarchar](max) NOT NULL,
    [Status] [tinyint] NOT NULL CONSTRAINT [DF_Bugs_Status] DEFAULT (0),
    [RiskLevel] [tinyint] NOT NULL CONSTRAINT [DF_Bugs_RiskLevel] DEFAULT (0),
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Bugs_CreatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_Bugs] PRIMARY KEY CLUSTERED ([BugId] ASC)
)
GO

CREATE TABLE [dbo].[Notifications](
    [NotificationId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [NotificationType] [tinyint] NOT NULL,
    [Title] [nvarchar](255) NULL,
    [Message] [nvarchar](max) NULL,
    [RelatedTaskId] [int] NULL,
    [RelatedProjectId] [int] NULL,
    [IsRead] [bit] NOT NULL CONSTRAINT [DF_Notifications_IsRead] DEFAULT (0),
    [ReadAt] [datetime2](7) NULL,
    [SentAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Notifications_SentAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_Notifications] PRIMARY KEY CLUSTERED ([NotificationId] ASC)
)
GO

CREATE TABLE [dbo].[CommunicationLog](
    [CommunicationId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [SentBy] [int] NOT NULL,
    [RecipientUserId] [int] NULL,
    [RecipientDepartmentId] [int] NULL,
    [CommunicationType] [tinyint] NOT NULL,
    [Subject] [nvarchar](255) NOT NULL,
    [Body] [nvarchar](max) NOT NULL,
    [SentAt] [datetime2](7) NOT NULL CONSTRAINT [DF_CommunicationLog_SentAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_CommunicationLog] PRIMARY KEY CLUSTERED ([CommunicationId] ASC)
)
GO

CREATE TABLE [dbo].[AuditLogs](
    [LogId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [Action] [nvarchar](100) NOT NULL,
    [ResourceType] [nvarchar](100) NOT NULL,
    [ResourceId] [int] NULL,
    [ChangesJson] [nvarchar](max) NULL,
    [IpAddress] [nvarchar](45) NULL,
    [UserAgent] [nvarchar](500) NULL,
    [Timestamp] [datetime2](7) NOT NULL CONSTRAINT [DF_AuditLogs_Timestamp] DEFAULT (getutcdate()),
 CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([LogId] ASC)
)
GO

CREATE TABLE [dbo].[AIActionsLog](
    [AiLogId] [int] IDENTITY(1,1) NOT NULL,
    [ActionType] [tinyint] NOT NULL,
    [TaskId] [int] NULL,
    [UserId] [int] NULL,
    [InputData] [nvarchar](max) NULL,
    [OutputData] [nvarchar](max) NULL,
    [ModelVersion] [nvarchar](50) NULL,
    [Timestamp] [datetime2](7) NOT NULL CONSTRAINT [DF_AIActionsLog_Timestamp] DEFAULT (getutcdate()),
 CONSTRAINT [PK_AIActionsLog] PRIMARY KEY CLUSTERED ([AiLogId] ASC)
)
GO

CREATE TABLE [dbo].[Reports](
    [ReportId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [GeneratedBy] [int] NOT NULL,
    [ReportType] [tinyint] NOT NULL,
    [Title] [nvarchar](255) NULL,
    [DataJson] [nvarchar](max) NULL,
    [FilePath] [nvarchar](500) NULL,
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_Reports_CreatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_Reports] PRIMARY KEY CLUSTERED ([ReportId] ASC)
)
GO

CREATE TABLE [dbo].[RolePermissions](
    [PermissionId] [int] IDENTITY(1,1) NOT NULL,
    [CompanyId] [int] NOT NULL,
    [Role] [tinyint] NOT NULL,
    [Resource] [nvarchar](100) NOT NULL,
    [Action] [nvarchar](50) NOT NULL,
    [CanPerform] [bit] NOT NULL CONSTRAINT [DF_RolePermissions_CanPerform] DEFAULT (1),
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_RolePermissions_CreatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([PermissionId] ASC)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_RolePermissions] ON [dbo].[RolePermissions]([CompanyId] ASC, [Role] ASC, [Resource] ASC, [Action] ASC)
GO

CREATE TABLE [dbo].[ChatThreads](
    [ThreadId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [Title] [nvarchar](255) NOT NULL CONSTRAINT [DF_ChatThreads_Title] DEFAULT (N'New Chat'),
    [Status] [tinyint] NOT NULL CONSTRAINT [DF_ChatThreads_Status] DEFAULT (0),
    [RelatedTaskId] [int] NULL,
    [RelatedProjectId] [int] NULL,
    [CreatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChatThreads_CreatedAt] DEFAULT (getutcdate()),
    [UpdatedAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChatThreads_UpdatedAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_ChatThreads] PRIMARY KEY CLUSTERED ([ThreadId] ASC)
)
GO

CREATE NONCLUSTERED INDEX [IX_ChatThreads_UserId] ON [dbo].[ChatThreads]([UserId] ASC)
GO
CREATE NONCLUSTERED INDEX [IX_ChatThreads_UserId_Status] ON [dbo].[ChatThreads]([UserId] ASC, [Status] ASC)
GO
CREATE NONCLUSTERED INDEX [IX_ChatThreads_UpdatedAt] ON [dbo].[ChatThreads]([UpdatedAt] DESC)
GO
CREATE NONCLUSTERED INDEX [IX_ChatThreads_RelatedTaskId] ON [dbo].[ChatThreads]([RelatedTaskId] ASC) WHERE ([RelatedTaskId] IS NOT NULL)
GO

CREATE TABLE [dbo].[ChatMessages](
    [MessageId] [int] IDENTITY(1,1) NOT NULL,
    [ThreadId] [int] NOT NULL,
    [Role] [tinyint] NOT NULL,
    [Content] [nvarchar](max) NOT NULL,
    [ModelVersion] [nvarchar](50) NULL,
    [TokensUsed] [int] NULL,
    [IsDeleted] [bit] NOT NULL CONSTRAINT [DF_ChatMessages_IsDeleted] DEFAULT (0),
    [SentAt] [datetime2](7) NOT NULL CONSTRAINT [DF_ChatMessages_SentAt] DEFAULT (getutcdate()),
 CONSTRAINT [PK_ChatMessages] PRIMARY KEY CLUSTERED ([MessageId] ASC)
)
GO

CREATE NONCLUSTERED INDEX [IX_ChatMessages_ThreadId] ON [dbo].[ChatMessages]([ThreadId] ASC)
GO
CREATE NONCLUSTERED INDEX [IX_ChatMessages_ThreadId_SentAt] ON [dbo].[ChatMessages]([ThreadId] ASC, [SentAt] ASC) WHERE ([IsDeleted]=(0))
GO

CREATE TABLE [dbo].[TaskComments](
    [CommentId] [int] IDENTITY(1,1) NOT NULL,
    [TaskId] [int] NOT NULL,
    [UserId] [int] NOT NULL,
    [Content] [nvarchar](2000) NOT NULL,
    [CreatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_TaskComments] PRIMARY KEY CLUSTERED ([CommentId] ASC)
)
GO

CREATE TABLE [dbo].[UserSettings](
    [UserSettingId] [int] IDENTITY(1,1) NOT NULL,
    [UserId] [int] NOT NULL,
    [SettingsJson] [nvarchar](max) NULL,
    [UpdatedAt] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_UserSettings] PRIMARY KEY CLUSTERED ([UserSettingId] ASC)
)
GO

CREATE UNIQUE NONCLUSTERED INDEX [UQ_UserSettings_UserId] ON [dbo].[UserSettings]([UserId] ASC)
GO

-- Foreign Keys
ALTER TABLE [dbo].[Departments] ADD CONSTRAINT [FK_Departments_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[Positions] ADD CONSTRAINT [FK_Positions_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[Positions] ADD CONSTRAINT [FK_Positions_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [dbo].[Departments]([DepartmentId])
GO
ALTER TABLE [dbo].[Positions] ADD CONSTRAINT [FK_Positions_ReportsTo] FOREIGN KEY([ReportsTo]) REFERENCES [dbo].[Positions]([PositionId])
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [FK_Users_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [FK_Users_Departments] FOREIGN KEY([DepartmentId]) REFERENCES [dbo].[Departments]([DepartmentId])
GO
ALTER TABLE [dbo].[Users] ADD CONSTRAINT [FK_Users_Positions] FOREIGN KEY([PositionId]) REFERENCES [dbo].[Positions]([PositionId])
GO
ALTER TABLE [dbo].[Projects] ADD CONSTRAINT [FK_Projects_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[Projects] ADD CONSTRAINT [FK_Projects_Manager] FOREIGN KEY([ManagerId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Tasks] ADD CONSTRAINT [FK_Tasks_Projects] FOREIGN KEY([ProjectId]) REFERENCES [dbo].[Projects]([ProjectId])
GO
ALTER TABLE [dbo].[Tasks] ADD CONSTRAINT [FK_Tasks_AssignedTo] FOREIGN KEY([AssignedTo]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Tasks] ADD CONSTRAINT [FK_Tasks_CreatedBy] FOREIGN KEY([CreatedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Subtasks] ADD CONSTRAINT [FK_Subtasks_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TaskProgress] ADD CONSTRAINT [FK_TaskProgress_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[TaskProgress] ADD CONSTRAINT [FK_TaskProgress_SubmittedBy] FOREIGN KEY([SubmittedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[TaskAttachments] ADD CONSTRAINT [FK_TaskAttachments_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[TaskAttachments] ADD CONSTRAINT [FK_TaskAttachments_UploadedBy] FOREIGN KEY([UploadedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[TaskAttachments] ADD CONSTRAINT [FK_TaskAttachments_Progress] FOREIGN KEY([ProgressId]) REFERENCES [dbo].[TaskProgress]([ProgressId])
GO
ALTER TABLE [dbo].[TaskAccessControl] ADD CONSTRAINT [FK_TaskAccessControl_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TaskAccessControl] ADD CONSTRAINT [FK_TaskAccessControl_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[TaskAccessControl] ADD CONSTRAINT [FK_TaskAccessControl_GrantedBy] FOREIGN KEY([GrantedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[TaskDependencies] ADD CONSTRAINT [FK_TaskDependencies_Task] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TaskDependencies] ADD CONSTRAINT [FK_TaskDependencies_DependsOnTask] FOREIGN KEY([DependsOnTaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [FK_Evaluations_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [FK_Evaluations_EvaluatedUser] FOREIGN KEY([EvaluatedUserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [FK_Evaluations_Evaluator] FOREIGN KEY([EvaluatorId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Bugs] ADD CONSTRAINT [FK_Bugs_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[Bugs] ADD CONSTRAINT [FK_Bugs_ReportedBy] FOREIGN KEY([ReportedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Notifications] ADD CONSTRAINT [FK_Notifications_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[CommunicationLog] ADD CONSTRAINT [FK_CommunicationLog_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[CommunicationLog] ADD CONSTRAINT [FK_CommunicationLog_SentBy] FOREIGN KEY([SentBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[CommunicationLog] ADD CONSTRAINT [FK_CommunicationLog_RecipientUser] FOREIGN KEY([RecipientUserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[CommunicationLog] ADD CONSTRAINT [FK_CommunicationLog_RecipientDepartment] FOREIGN KEY([RecipientDepartmentId]) REFERENCES [dbo].[Departments]([DepartmentId])
GO
ALTER TABLE [dbo].[AuditLogs] ADD CONSTRAINT [FK_AuditLogs_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[AIActionsLog] ADD CONSTRAINT [FK_AIActionsLog_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[AIActionsLog] ADD CONSTRAINT [FK_AIActionsLog_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[Reports] ADD CONSTRAINT [FK_Reports_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId])
GO
ALTER TABLE [dbo].[Reports] ADD CONSTRAINT [FK_Reports_GeneratedBy] FOREIGN KEY([GeneratedBy]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[RolePermissions] ADD CONSTRAINT [FK_RolePermissions_Companies] FOREIGN KEY([CompanyId]) REFERENCES [dbo].[Companies]([CompanyId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ChatThreads] ADD CONSTRAINT [FK_ChatThreads_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[ChatThreads] ADD CONSTRAINT [FK_ChatThreads_Tasks] FOREIGN KEY([RelatedTaskId]) REFERENCES [dbo].[Tasks]([TaskId])
GO
ALTER TABLE [dbo].[ChatThreads] ADD CONSTRAINT [FK_ChatThreads_Projects] FOREIGN KEY([RelatedProjectId]) REFERENCES [dbo].[Projects]([ProjectId])
GO
ALTER TABLE [dbo].[ChatMessages] ADD CONSTRAINT [FK_ChatMessages_ChatThreads] FOREIGN KEY([ThreadId]) REFERENCES [dbo].[ChatThreads]([ThreadId])
GO
ALTER TABLE [dbo].[TaskComments] ADD CONSTRAINT [FK_TaskComments_Tasks] FOREIGN KEY([TaskId]) REFERENCES [dbo].[Tasks]([TaskId]) ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TaskComments] ADD CONSTRAINT [FK_TaskComments_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId])
GO
ALTER TABLE [dbo].[UserSettings] ADD CONSTRAINT [FK_UserSettings_Users] FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([UserId]) ON DELETE CASCADE
GO

-- Check Constraints
ALTER TABLE [dbo].[Tasks] ADD CONSTRAINT [CK_Tasks_CompletionPercentage] CHECK (([CompletionPercentage]>=(0) AND [CompletionPercentage]<=(100)))
GO
ALTER TABLE [dbo].[Tasks] ADD CONSTRAINT [CK_Tasks_Dates] CHECK (([DueDate]>=[StartDate]))
GO
ALTER TABLE [dbo].[TaskProgress] ADD CONSTRAINT [CK_TaskProgress_Completion] CHECK (([CompletionPercentage]>=(0) AND [CompletionPercentage]<=(100)))
GO
ALTER TABLE [dbo].[TaskDependencies] ADD CONSTRAINT [CK_TaskDependencies_NoSelf] CHECK (([TaskId]<>[DependsOnTaskId]))
GO
ALTER TABLE [dbo].[Projects] ADD CONSTRAINT [CK_Projects_Dates] CHECK (([EndDate]>=[StartDate]))
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [CK_Evaluations_Overall] CHECK (([OverallScore]>=(0) AND [OverallScore]<=(100)))
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [CK_Evaluations_Quality] CHECK (([QualityScore]>=(0) AND [QualityScore]<=(100)))
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [CK_Evaluations_Timeliness] CHECK (([TimelinessScore]>=(0) AND [TimelinessScore]<=(100)))
GO
ALTER TABLE [dbo].[Evaluations] ADD CONSTRAINT [CK_Evaluations_Collaboration] CHECK (([CollaborationScore]>=(0) AND [CollaborationScore]<=(100)))
GO
ALTER TABLE [dbo].[ChatMessages] ADD CONSTRAINT [CK_ChatMessages_Role] CHECK (([Role]=(2) OR [Role]=(1) OR [Role]=(0)))
GO

-- ============================================================
-- SEED DATA
-- ============================================================

SET IDENTITY_INSERT [dbo].[Companies] ON
INSERT INTO [dbo].[Companies] ([CompanyId], [CompanyName], [IndustryPrimary], [IndustrySecondary], [SettingsJson], [CreatedAt], [UpdatedAt])
VALUES (1, N'TaskTune Demo Corp', N'Technology', N'Software Development', N'{"theme":"default"}', GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Companies] OFF
GO

SET IDENTITY_INSERT [dbo].[Departments] ON
INSERT INTO [dbo].[Departments] ([DepartmentId], [CompanyId], [DepartmentName], [Description], [CreatedAt], [UpdatedAt]) VALUES
(1, 1, N'Engineering', N'Software development and engineering', GETUTCDATE(), GETUTCDATE()),
(2, 1, N'Product', N'Product management and strategy', GETUTCDATE(), GETUTCDATE()),
(3, 1, N'Human Resources', N'HR operations and employee management', GETUTCDATE(), GETUTCDATE()),
(4, 1, N'Marketing', N'Marketing and growth', GETUTCDATE(), GETUTCDATE()),
(5, 1, N'Operations', N'Business operations', GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Departments] OFF
GO

SET IDENTITY_INSERT [dbo].[Positions] ON
INSERT INTO [dbo].[Positions] ([PositionId], [CompanyId], [DepartmentId], [PositionTitle], [Description], [ReportsTo], [CreatedAt], [UpdatedAt]) VALUES
(1, 1, NULL, N'Chief Executive Officer', N'Company CEO', NULL, GETUTCDATE(), GETUTCDATE()),
(2, 1, 1, N'Engineering Manager', N'Manages engineering team', 1, GETUTCDATE(), GETUTCDATE()),
(3, 1, 1, N'Team Lead', N'Leads a dev team', 2, GETUTCDATE(), GETUTCDATE()),
(4, 1, 1, N'Software Engineer', N'Individual contributor', 3, GETUTCDATE(), GETUTCDATE()),
(5, 1, 3, N'HR Director', N'Heads HR department', 1, GETUTCDATE(), GETUTCDATE()),
(6, 1, NULL, N'System Administrator', N'System admin', 1, GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Positions] OFF
GO

-- Passwords: 'Password123!' hashed with BCrypt
SET IDENTITY_INSERT [dbo].[Users] ON
INSERT INTO [dbo].[Users] ([UserId],[CompanyId],[DepartmentId],[PositionId],[Email],[PasswordHash],[FirstName],[LastName],[Phone],[Role],[IsActive],[CreatedAt],[UpdatedAt]) VALUES
(1, 1, 1, 4, N'employee@tasktune.ai',    N'Password123!', N'Ahmed',  N'Hassan',  N'+201000000001', 0, 1, GETUTCDATE(), GETUTCDATE()),
(2, 1, 1, 3, N'supervisor@tasktune.ai',   N'Password123!', N'Sara',   N'Ibrahim', N'+201000000002', 1, 1, GETUTCDATE(), GETUTCDATE()),
(3, 1, 1, 2, N'manager@tasktune.ai',      N'Password123!', N'Omar',   N'Khalil',  N'+201000000003', 2, 1, GETUTCDATE(), GETUTCDATE()),
(4, 1, NULL, 1, N'ceo@tasktune.ai',       N'Password123!', N'Layla',  N'Mansour', N'+201000000004', 4, 1, GETUTCDATE(), GETUTCDATE()),
(5, 1, 3, 5, N'hr@tasktune.ai',           N'Password123!', N'Nour',   N'Farid',   N'+201000000005', 3, 1, GETUTCDATE(), GETUTCDATE()),
(6, 1, NULL, 6, N'admin@tasktune.ai',     N'Password123!', N'Youssef',N'Amin',    N'+201000000006', 5, 1, GETUTCDATE(), GETUTCDATE()),
(7, 1, 1, 4, N'employee2@tasktune.ai',    N'Password123!', N'Mona',   N'Saeed',   N'+201000000007', 0, 1, GETUTCDATE(), GETUTCDATE()),
(8, 1, 2, 4, N'employee3@tasktune.ai',    N'Password123!', N'Tarek',  N'Ali',     N'+201000000008', 0, 1, GETUTCDATE(), GETUTCDATE()),
(9, 1, 4, 4, N'employee4@tasktune.ai',    N'Password123!', N'Hana',   N'Mostafa', N'+201000000009', 0, 1, GETUTCDATE(), GETUTCDATE()),
(10,1, 5, 4, N'employee5@tasktune.ai',    N'Password123!', N'Karim',  N'Nabil',   N'+201000000010', 0, 1, GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Users] OFF
GO

SET IDENTITY_INSERT [dbo].[Projects] ON
INSERT INTO [dbo].[Projects] ([ProjectId],[CompanyId],[ProjectName],[Description],[ManagerId],[Status],[StartDate],[EndDate],[CreatedAt],[UpdatedAt]) VALUES
(1, 1, N'Website Redesign',     N'Complete redesign of the company website with modern UI/UX', 3, 1, '2026-01-15', '2026-06-30', GETUTCDATE(), GETUTCDATE()),
(2, 1, N'Mobile App Development', N'Build cross-platform mobile application for task management', 3, 1, '2026-02-01', '2026-08-31', GETUTCDATE(), GETUTCDATE()),
(3, 1, N'API Integration',      N'Integrate third-party APIs for enhanced functionality',       3, 0, '2026-03-01', '2026-05-15', GETUTCDATE(), GETUTCDATE()),
(4, 1, N'Data Analytics Platform', N'Build internal analytics dashboard for business metrics',  3, 0, '2026-04-01', '2026-09-30', GETUTCDATE(), GETUTCDATE()),
(5, 1, N'Security Audit',       N'Complete security audit and implement recommendations',       3, 2, '2025-10-01', '2026-01-31', GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Projects] OFF
GO

SET IDENTITY_INSERT [dbo].[Tasks] ON
INSERT INTO [dbo].[Tasks] ([TaskId],[ProjectId],[TaskName],[Description],[AssignedTo],[CreatedBy],[Status],[Priority],[CompletionPercentage],[StartDate],[DueDate],[CreatedAt],[UpdatedAt]) VALUES
(1, 1, N'Design Homepage Layout',    N'Create wireframes and mockups for the new homepage',        1, 2, 1, 2, 60, '2026-01-20', '2026-02-28', GETUTCDATE(), GETUTCDATE()),
(2, 1, N'Implement Auth Module',     N'Build login, registration and password reset functionality', 7, 2, 0, 2, 0,  '2026-02-01', '2026-03-15', GETUTCDATE(), GETUTCDATE()),
(3, 2, N'Setup React Native Project',N'Initialize the mobile app project with base configuration',  1, 2, 2, 1, 100,'2026-02-01', '2026-02-15', GETUTCDATE(), GETUTCDATE()),
(4, 2, N'Build User Dashboard',      N'Create the main dashboard screen for mobile app',           8, 2, 1, 2, 35, '2026-02-15', '2026-03-30', GETUTCDATE(), GETUTCDATE()),
(5, 3, N'Research Payment APIs',     N'Evaluate Stripe, PayPal, and local payment gateways',       9, 3, 1, 1, 50, '2026-03-01', '2026-03-31', GETUTCDATE(), GETUTCDATE()),
(6, 1, N'Responsive CSS Fixes',      N'Fix layout issues on tablet and mobile breakpoints',        7, 2, 0, 0, 0,  '2026-03-01', '2026-04-15', GETUTCDATE(), GETUTCDATE()),
(7, 4, N'Setup Data Pipeline',       N'Configure ETL pipeline for analytics data ingestion',       10,3, 0, 2, 0,  '2026-04-01', '2026-05-15', GETUTCDATE(), GETUTCDATE()),
(8, 5, N'Penetration Testing Report',N'Document findings from the security penetration testing',    1, 3, 2, 3, 100,'2025-11-01', '2025-12-31', GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Tasks] OFF
GO

SET IDENTITY_INSERT [dbo].[Subtasks] ON
INSERT INTO [dbo].[Subtasks] ([SubtaskId],[TaskId],[SubtaskName],[Description],[Status],[OrderIndex],[CreatedAt],[UpdatedAt]) VALUES
(1, 1, N'Create wireframes',       N'Low-fidelity wireframes for homepage sections',  2, 0, GETUTCDATE(), GETUTCDATE()),
(2, 1, N'Design hero section',     N'High-fidelity design for hero banner',           1, 1, GETUTCDATE(), GETUTCDATE()),
(3, 1, N'Design navigation bar',   N'Responsive nav with dropdown menus',             0, 2, GETUTCDATE(), GETUTCDATE()),
(4, 2, N'Setup auth endpoints',    N'Create login/register API endpoints',             0, 0, GETUTCDATE(), GETUTCDATE()),
(5, 2, N'Build login form UI',     N'React login form with validation',               0, 1, GETUTCDATE(), GETUTCDATE()),
(6, 4, N'Dashboard wireframe',     N'Create mobile dashboard layout',                 1, 0, GETUTCDATE(), GETUTCDATE()),
(7, 4, N'Implement chart widgets', N'Add chart components using charting library',     0, 1, GETUTCDATE(), GETUTCDATE())
SET IDENTITY_INSERT [dbo].[Subtasks] OFF
GO

INSERT INTO [dbo].[Evaluations] ([TaskId],[EvaluatedUserId],[EvaluatorId],[OverallScore],[QualityScore],[TimelinessScore],[CollaborationScore],[RiskLevel],[ManualFeedback],[AiGeneratedFeedback],[Status],[SubmittedAt],[CreatedAt]) VALUES
(3, 1, 2, 88, 90, 85, 90, 0, N'Excellent work on project setup. Clean architecture and good documentation.', N'The employee demonstrated strong technical skills in project initialization.', 1, GETUTCDATE(), GETUTCDATE()),
(8, 1, 3, 92, 95, 88, 93, 0, N'Thorough security testing with detailed report. Found critical vulnerabilities.', N'Outstanding performance in security analysis. Proactive identification of issues.', 1, GETUTCDATE(), GETUTCDATE())
GO

INSERT INTO [dbo].[Notifications] ([UserId],[NotificationType],[Title],[Message],[RelatedTaskId],[RelatedProjectId],[IsRead],[SentAt]) VALUES
(1, 0, N'New Task Assigned',         N'You have been assigned to "Design Homepage Layout"',              1, 1, 0, GETUTCDATE()),
(1, 1, N'Task Due Soon',             N'Task "Design Homepage Layout" is due in 3 days',                  1, 1, 0, GETUTCDATE()),
(1, 2, N'Evaluation Received',       N'You received a new evaluation for "Setup React Native Project"',  3, 2, 1, GETUTCDATE()),
(2, 0, N'Progress Submitted',        N'Ahmed Hassan submitted progress on "Design Homepage Layout"',     1, 1, 0, GETUTCDATE()),
(3, 3, N'Project Update',            N'Project "Website Redesign" status has been updated',              NULL, 1, 0, GETUTCDATE()),
(4, 3, N'Monthly Report Ready',      N'Your monthly executive report is ready for review',               NULL,NULL, 0, GETUTCDATE()),
(5, 4, N'Employee Warning',          N'Employee performance alert for review',                           NULL,NULL, 0, GETUTCDATE()),
(6, 5, N'System Alert',              N'Database backup completed successfully',                          NULL,NULL, 1, GETUTCDATE())
GO

INSERT INTO [dbo].[TaskProgress] ([TaskId],[SubmittedBy],[CompletionPercentage],[Notes],[Blockers],[SubmittedAt]) VALUES
(1, 1, 40, N'Completed wireframes for all major sections. Moving to high-fidelity designs.', NULL, DATEADD(day, -5, GETUTCDATE())),
(1, 1, 60, N'Hero section design complete. Working on navigation and footer.', N'Waiting for brand guidelines from marketing team.', GETUTCDATE()),
(4, 8, 35, N'Dashboard wireframe approved. Starting chart widget implementation.', NULL, GETUTCDATE()),
(5, 9, 50, N'Completed evaluation of Stripe and PayPal. Starting local gateway research.', NULL, GETUTCDATE())
GO

INSERT INTO [dbo].[AuditLogs] ([UserId],[Action],[ResourceType],[ResourceId],[ChangesJson],[IpAddress],[Timestamp]) VALUES
(6, N'LOGIN', N'Auth', NULL, NULL, N'127.0.0.1', GETUTCDATE()),
(2, N'CREATE', N'Task', 1, N'{"TaskName":"Design Homepage Layout"}', N'127.0.0.1', GETUTCDATE()),
(1, N'UPDATE', N'TaskProgress', 1, N'{"CompletionPercentage":60}', N'127.0.0.1', GETUTCDATE()),
(3, N'CREATE', N'Project', 3, N'{"ProjectName":"API Integration"}', N'127.0.0.1', GETUTCDATE())
GO

-- Seed Reports
INSERT INTO [dbo].[Reports] ([CompanyId],[GeneratedBy],[ReportType],[ReportPeriodStart],[ReportPeriodEnd],[FilePath],[MetadataJson],[GeneratedAt]) VALUES
(1, 3, 0, '2026-01-01', '2026-01-31', NULL, N'{"chartData":[{"dept":"Engineering","score":85},{"dept":"Product","score":78},{"dept":"Marketing","score":72},{"dept":"HR","score":90},{"dept":"Operations","score":68}]}', DATEADD(day, -30, GETUTCDATE())),
(1, 3, 1, '2026-02-01', '2026-02-28', NULL, N'{"chartData":[{"dept":"Engineering","score":92},{"dept":"Product","score":80},{"dept":"Marketing","score":75},{"dept":"HR","score":88}]}', DATEADD(day, -15, GETUTCDATE())),
(1, 4, 2, '2026-01-01', '2026-03-15', NULL, N'{"chartData":[{"dept":"Website Redesign","score":65},{"dept":"Mobile App","score":45},{"dept":"API Integration","score":30},{"dept":"Data Analytics","score":10},{"dept":"Security Audit","score":100}]}', DATEADD(day, -5, GETUTCDATE()))
GO

-- Seed Communications
INSERT INTO [dbo].[CommunicationLog] ([CompanyId],[SentBy],[RecipientUserId],[RecipientDepartmentId],[CommunicationType],[Subject],[Body],[SentAt]) VALUES
(1, 5, 1, NULL, 0, N'Welcome to TaskTune!', N'Welcome aboard! Please review the onboarding documents and complete your profile setup.', DATEADD(day, -20, GETUTCDATE())),
(1, 5, NULL, 1, 1, N'Engineering Team Update', N'Please note the updated sprint schedule. All team members should update their task progress by Friday.', DATEADD(day, -14, GETUTCDATE())),
(1, 3, 2, NULL, 0, N'Project Review Meeting', N'Please prepare your status reports for the weekly project review meeting scheduled for Monday.', DATEADD(day, -7, GETUTCDATE())),
(1, 4, NULL, NULL, 2, N'Company All-Hands Meeting', N'Reminder: The quarterly all-hands meeting is scheduled for next Thursday at 2 PM. Attendance is mandatory.', DATEADD(day, -3, GETUTCDATE())),
(1, 5, 7, NULL, 0, N'Performance Review Reminder', N'Your annual performance review is scheduled for next week. Please ensure your self-assessment form is completed.', DATEADD(day, -1, GETUTCDATE()))
GO

-- Seed Task Comments
INSERT INTO [dbo].[TaskComments] ([TaskId],[UserId],[Content],[CreatedAt]) VALUES
(1, 2, N'Great progress on the wireframes! The layout looks clean.', DATEADD(day, -4, GETUTCDATE())),
(1, 1, N'Thanks! Working on the hero section now. Should have it done by tomorrow.', DATEADD(day, -3, GETUTCDATE())),
(1, 3, N'Make sure the navigation follows the new brand guidelines.', DATEADD(day, -2, GETUTCDATE())),
(4, 8, N'Dashboard wireframe is ready for review. Let me know your feedback.', DATEADD(day, -2, GETUTCDATE())),
(4, 2, N'Looks good! Proceed with chart implementation. Use recharts for consistency.', DATEADD(day, -1, GETUTCDATE())),
(5, 9, N'Stripe integration looks most promising. Their API is well-documented.', DATEADD(day, -1, GETUTCDATE()))
GO

PRINT 'Database setup complete with seed data.'
GO
