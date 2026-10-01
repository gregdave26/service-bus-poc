--Clean up script if required
DROP procedure IF EXISTS [dbo].[sp_truncateStaging_AgentInfo]
DROP procedure IF EXISTS [dbo].[sp_commit_AgentInfo]
DROP table IF EXISTS [dbo].[IEX_AgentInfo]
DROP table IF EXISTS [dbo].[staging_IEX_AgentInfo]
go

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[staging_IEX_AgentInfo](
	[id] INT IDENTITY(1,1) PRIMARY KEY,	
	[muID] [varchar](8) NULL,
	[muName] [varchar](32) NULL,
	[date] [varchar](16) NULL,
	[logonID] [varchar](16) NULL,
	[agentName] [varchar](256) NULL,
	[agentDataAwayLat] [varchar](24) NULL,
	[agentDataAwayLong] [varchar](24) NULL,
	[agentDataAwayLoc] [varchar](64) NULL,
	[agentDataUserID] [varchar](16) NULL,
	[agentDataMealType] [varchar](16) NULL,
	[agentDataUnitID] [varchar](16) NULL,
	[agentDataVehicleID] [varchar](16) NULL,
	[startDate] [varchar](16) NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[staging_IEX_AgentInfo] ADD  CONSTRAINT [df_adf_iex_agentinfo_RecordInsertTimestamp]  DEFAULT (getutcdate()) FOR [RecordInsertTimestamp]
GO


SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_AgentInfo](
	[muID] [varchar](8) NULL,
	[muName] [varchar](32) NULL,
	[date] [datetime2](7) NULL,
	[logonID] [varchar](16) NULL,
	[agentFirstName] [varchar](64) NULL,
	[agentLastName] [varchar](64) NULL,
	[agentIdentifyNumber] [varchar](16) NULL,
	[agentDataAwayXCoord] [decimal](12, 4) NULL,
	[agentDataAwayYCoord] [decimal](12, 4) NULL,
	[agentDataAwayLoc] [varchar](64) NULL,
	[agentDataUserID] [varchar](16) NULL,
	[agentDataMealType] [varchar](16) NULL,
	[agentDataUnitID] [varchar](16) NULL,
	[agentDataVehicleID] [varchar](16) NULL,
	[startDate] [datetime] NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

--truncate SP
--procedure to truncate staging table
CREATE PROCEDURE [dbo].[sp_truncateStaging_AgentInfo]
WITH EXECUTE AS OWNER
AS
BEGIN
	TRUNCATE TABLE [dbo].[staging_IEX_AgentInfo];
END
GO


SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[sp_commit_AgentInfo] (@activeDateString varchar(16))
AS
    SET NOCOUNT ON
    ;
	DECLARE @activeDate DATETIME = DATETIMEFROMPARTS(CONCAT('20', SUBSTRING(@activeDateString, 5, 2)), SUBSTRING(@activeDateString, 1, 2), SUBSTRING(@activeDateString, 3, 2), '0', '0', '0', '0');

	DECLARE @ApplicableMUGroupIDs TABLE (muGroupID varchar(8));
	INSERT INTO @ApplicableMUGroupIDs VALUES ('4000'), ('4001'), ('4002');

	SELECT *
	INTO #applicableRecords
	FROM [dbo].[staging_IEX_AgentInfo]
	WHERE muID in (SELECT muGroupID FROM @ApplicableMUGroupIDs)

	SELECT * 
	INTO #validRecords
	FROM #applicableRecords 
	WHERE #applicableRecords.AgentName LIKE '%#%,%' ;

	SELECT DISTINCT [AgentName] 
	INTO #invalidRecords
	FROM #applicableRecords 
	WHERE #applicableRecords.AgentName NOT LIKE '%#%,%';


    -- Merge to base table
    MERGE [dbo].[IEX_AgentInfo] AS D
    USING #validRecords AS S
    ON
		D.[logonID] = S.[logonID]
    WHEN MATCHED
	AND S.[muID] IN (SELECT muGroupID FROM @ApplicableMUGroupIDs)
	AND @activeDate = DATETIMEFROMPARTS(SUBSTRING(S.[date], 5, 4), SUBSTRING(S.[date], 1, 2), SUBSTRING(S.[date], 3, 2), '0', '0', '0', '0')
	THEN
        UPDATE SET
			D.[muID] = S.[muID]
			,D.[muName] = s.[muName]
			,D.[date] = DATETIMEFROMPARTS(SUBSTRING(S.[date], 5, 4), SUBSTRING(S.[date], 1, 2), SUBSTRING(S.[date], 3, 2), '0', '0', '0', '0')
			,D.[AgentFirstName] = RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,D.[AgentLastName] = SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,D.[AgentIdentifyNumber] = LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,D.[agentDataAwayXCoord] = CAST(S.agentDataAwayLat AS decimal(12,4))
			,D.[agentDataAwayYCoord] = CAST(S.agentDataAwayLong AS decimal(12,4))
			,D.[agentDataAwayLoc] = S.[agentDataAwayLoc]
			,D.[agentDataUserID] = S.[agentDataUserID]
			,D.[agentDataMealType] = S.[agentDataMealType]
			,D.[agentDataUnitID] = S.[agentDataUnitID]
			,D.[agentDataVehicleID] = S.[agentDataVehicleID]
			,D.[startDate] = DATETIMEFROMPARTS(SUBSTRING(S.[startDate], 5, 4), SUBSTRING(S.[startDate], 1, 2), SUBSTRING(S.[startDate], 3, 2), '0', '0', '0', '0')
			,D.[RecordInsertTimestamp] = S.[RecordInsertTimestamp]
    WHEN NOT MATCHED BY TARGET
	AND S.[muID] IN (SELECT muGroupID FROM @ApplicableMUGroupIDs)
	AND @activeDate = DATETIMEFROMPARTS(SUBSTRING(S.[date], 5, 4), SUBSTRING(S.[date], 1, 2), SUBSTRING(S.[date], 3, 2), '0', '0', '0', '0')
		THEN
        INSERT
        (
			[muID]
			,[muName]
			,[date]
			,[logonID]
			,[AgentFirstName]
			,[AgentLastName]
			,[AgentIdentifyNumber]
			,[agentDataAwayXCoord]
			,[agentDataAwayYCoord]
			,[agentDataAwayLoc]
			,[agentDataUserID]
			,[agentDataMealType]
			,[agentDataUnitID]
			,[agentDataVehicleID]
			,[startDate]
			,[RecordInsertTimestamp]
        )
        VALUES
        (
			S.[muID]
			,S.[muName]
			,DATETIMEFROMPARTS(SUBSTRING(S.[date], 5, 4), SUBSTRING(S.[date], 1, 2), SUBSTRING(S.[date], 3, 2), '0', '0', '0', '0')
			,S.[logonID]
			,RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,CAST(S.agentDataAwayLat AS decimal(12,4))
			,CAST(S.agentDataAwayLong AS decimal(12,4))
			,S.[agentDataAwayLoc]
			,S.[agentDataUserID]
			,S.[agentDataMealType]
			,S.[agentDataUnitID]
			,S.[agentDataVehicleID]
			,DATETIMEFROMPARTS(SUBSTRING(S.[startDate], 5, 4), SUBSTRING(S.[startDate], 1, 2), SUBSTRING(S.[startDate], 3, 2), '0', '0', '0', '0')
			,S.[RecordInsertTimestamp]
        )
    ;
    SELECT * FROM #invalidRecords
GO


