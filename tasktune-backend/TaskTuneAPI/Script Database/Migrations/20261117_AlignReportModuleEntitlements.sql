/*
  Align existing plan seed data to canonical module codes used by the API.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SubscriptionPlans', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.SubscriptionPlans
    SET EnabledModulesJson = REPLACE(EnabledModulesJson, N'"basic_reports"', N'"reports"'),
        UpdatedAt = SYSUTCDATETIME()
    WHERE EnabledModulesJson LIKE N'%"basic_reports"%';
END;

COMMIT TRANSACTION;
