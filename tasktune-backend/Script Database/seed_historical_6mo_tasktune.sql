USE [TaskTuneAIDatabase];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

PRINT 'Generating 6-Month Historical Data for ALL Company 6 Users...';

DECLARE @CompanyId INT = 6;
DECLARE @ManagerId INT = (SELECT TOP 1 UserId FROM Users WHERE Email = 'manager@tasktune.ai' AND CompanyId = @CompanyId);

-- Gather ALL users in Company 6 into a temporary table to iterate cleanly
IF OBJECT_ID('tempdb..#UsersToProcess') IS NOT NULL DROP TABLE #UsersToProcess;
SELECT ROW_NUMBER() OVER(ORDER BY UserId) AS RowNum, UserId 
INTO #UsersToProcess
FROM Users WHERE CompanyId = @CompanyId;

DECLARE @TotalUsers INT = (SELECT COUNT(*) FROM #UsersToProcess);
DECLARE @UIndex INT = 1;
DECLARE @CurrentUserId INT;

WHILE @UIndex <= @TotalUsers
BEGIN
    SELECT @CurrentUserId = UserId FROM #UsersToProcess WHERE RowNum = @UIndex;
    PRINT 'Processing Historicals for User: ' + CAST(@CurrentUserId AS VARCHAR);
    
    DECLARE @MonthOffset INT = 0; -- 0 to 5 (6 months total)
    
    WHILE @MonthOffset <= 5
    BEGIN
        DECLARE @BaseDate DATETIME = DATEADD(month, -@MonthOffset, GETDATE());
        DECLARE @ProjectOffset INT = (SELECT TOP 1 ProjectId FROM Projects WHERE CompanyId = @CompanyId ORDER BY NEWID());
        
        -- Insert 3 Tasks for this specific month
        DECLARE @TIndex INT = 1;
        DECLARE @InsertedTaskIdTable TABLE (TaskId INT);
        
        WHILE @TIndex <= 3
        BEGIN
            DECLARE @TaskStart DATETIME = DATEADD(day, -(@TIndex * 6), @BaseDate);
            DECLARE @TaskDue DATETIME = DATEADD(day, 3, @TaskStart);
            DECLARE @TaskStatus INT = IIF(@MonthOffset = 0 AND @TIndex = 3, 1, 2); -- If current month & last task, make it "In Progress", else Completed
            DECLARE @Pcnt INT = IIF(@TaskStatus = 2, 100, 50);
            
            INSERT INTO Tasks (ProjectId, TaskName, AssignedTo, Priority, Status, StartDate, DueDate, CreatedBy, CreatedAt, UpdatedAt, CompletionPercentage)
            OUTPUT inserted.TaskId INTO @InsertedTaskIdTable
            VALUES (
                @ProjectOffset, 
                'Historical Delivery M' + CAST(@MonthOffset AS VARCHAR) + ' T' + CAST(@TIndex AS VARCHAR), 
                @CurrentUserId, 1, @TaskStatus, 
                @TaskStart, @TaskDue, 
                @ManagerId, @TaskStart, @TaskDue, @Pcnt
            );
            
            SET @TIndex = @TIndex + 1;
        END

        -- Grab one of the newly inserted tasks for an Evaluation
        DECLARE @EvalTaskId INT = (SELECT TOP 1 TaskId FROM @InsertedTaskIdTable ORDER BY NEWID());
        
        -- Score starts higher recently and drops slightly historically
        DECLARE @HistScore INT = 85 + (5 - @MonthOffset) + (@UIndex % 5);
        IF @HistScore > 100 SET @HistScore = 98;
        
        IF @EvalTaskId IS NOT NULL
        BEGIN
            INSERT INTO Evaluations (TaskId, EvaluatedUserId, EvaluatorId, OverallScore, QualityScore, TimelinessScore, CollaborationScore, ManualFeedback, AiGeneratedFeedback, Status, CreatedAt)
            VALUES (
                @EvalTaskId, @CurrentUserId, @ManagerId, 
                @HistScore, @HistScore + 2, @HistScore - 2, @HistScore, 
                'Standard monthly review data plot.', 
                'AI analysis determines steady progress over this period.', 1, DATEADD(day, -2, @BaseDate)
            );
        END
        
        DELETE FROM @InsertedTaskIdTable;
        SET @MonthOffset = @MonthOffset + 1;
    END

    SET @UIndex = @UIndex + 1;
END

PRINT 'Historical Matrix Fully Generated.';
DROP TABLE #UsersToProcess;
GO