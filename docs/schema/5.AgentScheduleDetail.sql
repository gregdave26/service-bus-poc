--Script to store the Databa base objects related to the Agent Schedule Detail in the database.
--Clean up script if required
DROP procedure IF EXISTS [dbo].[sp_truncateStaging_AgentScheduleDetail]
DROP procedure IF EXISTS [dbo].[sp_commit_AgentScheduleDetail]
DROP table IF EXISTS [dbo].[IEX_AgentScheduleDetail]
DROP table IF EXISTS [dbo].[staging_IEX_AgentScheduleDetail]
go


--staging table
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[staging_IEX_AgentScheduleDetail](
	[id] INT IDENTITY(1,1) PRIMARY KEY, 
	[excDate] [varchar](16) NOT NULL,
	[schedDate] [varchar](16) NOT NULL,
	[muID] [varchar](8) NULL,
	[acdID] [varchar](8) NULL,
	[logonID] [varchar](16) NULL,
	[AgentName] [varchar](256) NULL,
	[ExceptionCode] [varchar](64) NULL,
	[StartTime] [varchar](8) NULL,
	[StopTime] [varchar](8) NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[staging_IEX_AgentScheduleDetail] ADD  CONSTRAINT [df_adf_iex_agentscheduledetail_RecordInsertTimestamp]  DEFAULT (getutcdate()) FOR [RecordInsertTimestamp]
GO

--commit table
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_AgentScheduleDetail](
	[excDate] [datetime2](7) NOT NULL,
	[schedDate] [datetime2](7) NOT NULL,
	[muID] [varchar](8) NULL,
	[acdID] [varchar](8) NULL,
	[logonID] [varchar](16) NULL,
	[AgentFirstName] [varchar](64) NULL,
	[AgentLastName] [varchar](64) NULL,
	[IdentifyNumber] [varchar](16) NULL,
	[ExceptionCode] [varchar](64) NULL,
	[StartTime] [time](7) NULL,
	[StopTime] [time](7) NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO


--truncate SP
--procedure to truncate staging table
CREATE PROCEDURE [dbo].[sp_truncateStaging_AgentScheduleDetail]
WITH EXECUTE AS OWNER
AS
BEGIN
	TRUNCATE TABLE [dbo].[staging_IEX_AgentScheduleDetail];
END
GO

-- commit SP

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[sp_commit_AgentScheduleDetail]
AS
	-- List of MUGroupIDs to filter on (4000-4002 refer to Patrols)
	DECLARE @ApplicableMUGroupIDs TABLE (muGroupID varchar(8));
	INSERT INTO @ApplicableMUGroupIDs VALUES ('4000'), ('4001'), ('4002');

    SET NOCOUNT ON
    ;

	SELECT *
	INTO #applicableRecords
	FROM [dbo].[staging_IEX_AgentScheduleDetail]
	WHERE muID in (SELECT muGroupID FROM @ApplicableMUGroupIDs)

	SELECT * 
	INTO #validRecords
	FROM #applicableRecords 
	WHERE #applicableRecords.AgentName LIKE '%#%,%' ;

	SELECT DISTINCT [AgentName] 
	INTO #invalidRecords
	FROM #applicableRecords 
	WHERE #applicableRecords.AgentName NOT LIKE '%#%,%';

	-- Clear any days that are found in the STAGING instance
	-- We want to replace all dates being staged, but no others
	DELETE
	FROM [dbo].[IEX_AgentScheduleDetail]
	WHERE excDate IN
	(
		SELECT DISTINCT
		DATETIMEFROMPARTS(SUBSTRING([excDate], 5, 4), SUBSTRING([excDate], 1, 2), SUBSTRING([excDate], 3, 2), '0', '0', '0', '0')
		FROM #validRecords
	)

    -- Merge to base table
    MERGE [dbo].[IEX_AgentScheduleDetail] AS D
    USING #validRecords AS S
    ON
		D.[excDate] = DATETIMEFROMPARTS(SUBSTRING(S.[excDate], 5, 4), SUBSTRING(S.[excDate], 1, 2), SUBSTRING(S.[excDate], 3, 2), '0', '0', '0', '0')
		AND D.[LogonID] = S.[logonID]
		AND D.[ExceptionCode] = S.[exceptionCode]
		AND D.[StartTime] = CONVERT(TIME, S.[StartTime])
    WHEN MATCHED
	AND S.[muID] IN (SELECT muGroupID FROM @ApplicableMUGroupIDs) -- Only merge the filtered rows
	THEN
		
        UPDATE SET
			D.[schedDate] = DATETIMEFROMPARTS(SUBSTRING(S.[schedDate], 5, 4), SUBSTRING(S.[schedDate], 1, 2), SUBSTRING(S.[schedDate], 3, 2), '0', '0', '0', '0')
			,D.[acdID] = S.[acdID]
			,D.[logonID] = S.[logonID]
			,D.[AgentFirstName] = RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,D.[AgentLastName] = SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,D.[IdentifyNumber] = LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,D.[ExceptionCode] = S.[ExceptionCode]
			,D.[StartTime] = S.[StartTime]
			,D.[StopTime] = S.[StopTime]
			,D.[RecordInsertTimestamp] = S.[RecordInsertTimestamp]

    WHEN NOT MATCHED BY TARGET
	AND S.[muID] IN (SELECT muGroupID FROM @ApplicableMUGroupIDs) -- Only merge the filtered rows
        THEN
        INSERT
        (
            [excDate]
			,[schedDate]
			,[muID]
			,[acdID]
			,[logonID]
			,[AgentFirstName]
			,[AgentLastName]
			,[IdentifyNumber]
			,[ExceptionCode]
			,[StartTime]
			,[StopTime]
			,[RecordInsertTimestamp]
        )
        VALUES
        (
            DATETIMEFROMPARTS(SUBSTRING(S.[excDate], 5, 4), SUBSTRING(S.[excDate], 1, 2), SUBSTRING(S.[excDate], 3, 2), '0', '0', '0', '0') 
			,DATETIMEFROMPARTS(SUBSTRING(S.[schedDate], 5, 4), SUBSTRING(S.[schedDate], 1, 2), SUBSTRING(S.[schedDate], 3, 2), '0', '0', '0', '0')
			,S.[muID]
			,S.[acdID]
			,S.[logonID]
			,RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,S.[ExceptionCode]
			,CONVERT(TIME, S.[StartTime])
			,CONVERT(TIME, S.[StopTime])
			,S.[RecordInsertTimestamp]
        )
    ;

	SELECT * FROM #invalidRecords
GO