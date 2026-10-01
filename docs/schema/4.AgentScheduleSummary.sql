--Script to store the Databa base objects related to the Agent Schedule Summary in the database.
--Clean up script if required
DROP procedure IF EXISTS [dbo].[sp_truncateStaging_AgentScheduleSummary]
DROP procedure IF EXISTS [dbo].[sp_commit_AgentScheduleSummary]
DROP table IF EXISTS [dbo].[IEX_AgentScheduleSummary]
DROP table IF EXISTS [dbo].[staging_IEX_AgentScheduleSummary]

-- staging table
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[staging_IEX_AgentScheduleSummary](
	[id] INT IDENTITY(1,1) PRIMARY KEY, 
	[Date] [varchar](16) NOT NULL,
	[MuID] [varchar](8) NOT NULL,
	[LogonID] [varchar](16) NULL,
	[AgentName] [varchar](256) NULL,
	[ExceptionCode] [varchar](64) NULL,
	[Minutes] [int] NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[staging_IEX_AgentScheduleSummary] ADD  CONSTRAINT [df_iex_agentScheduleSummary_RecordInsertTimestamp]  DEFAULT (getutcdate()) FOR [RecordInsertTimestamp]
GO
-- prefix with staging_ to differentiate between staging and production tables
-- production table
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_AgentScheduleSummary](
	[Date] [datetime2](7) NOT NULL,
	[MuID] [varchar](8) NOT NULL,
	[LogonID] [varchar](16) NULL,
	[AgentFirstName] [varchar](64) NULL,
	[AgentLastName] [varchar](64) NULL,
	[IdentifyNumber] [varchar](16) NULL,
	[ExceptionCode] [varchar](64) NULL,
	[Minutes] [int] NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

--Procedure to insert data from staging to production table
--prefix with sp_commit_
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[sp_commit_AgentScheduleSummary]
AS
    SET NOCOUNT ON
    ;

	-- Filtering on MUGroups (4000-4002 is for Patrols)
	DECLARE @ApplicableMUGroupIDs TABLE (muGroupID varchar(8));
	INSERT INTO @ApplicableMUGroupIDs VALUES ('4000'), ('4001'), ('4002');

	SELECT *
	INTO #applicableRecords
	FROM [dbo].[staging_IEX_AgentScheduleSummary]
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
    MERGE [dbo].[IEX_AgentScheduleSummary] AS D
    USING #validRecords AS S
    ON
		D.[Date] = DATETIMEFROMPARTS(SUBSTRING(S.[Date], 5, 4), SUBSTRING(S.[Date], 1, 2), SUBSTRING(S.[Date], 3, 2), '0', '0', '0', '0')
		AND D.[LogonID] = S.[LogonID]
		AND D.[ExceptionCode] = S.[ExceptionCode]
    WHEN MATCHED
		AND S.MuID in (SELECT muGroupID from @ApplicableMUGroupIDs) -- Must match filters
	THEN
        UPDATE SET
			D.[LogonID] = S.[LogonID]
			,D.[AgentFirstName] = RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,D.[AgentLastName] = SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,D.[IdentifyNumber] = LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,D.[ExceptionCode] = S.[ExceptionCode]
			,D.[Minutes] = S.[Minutes]
			,D.[RecordInsertTimestamp] = S.[RecordInsertTimestamp]

    WHEN NOT MATCHED BY TARGET
		AND S.muID in (SELECT muGroupID from @ApplicableMUGroupIDs) -- Must match filters
        THEN
        INSERT
        (
            [Date]
			,[MuID]
			,[LogonID]
			,[AgentFirstName]
			,[AgentLastName]
			,[IdentifyNumber]
			,[ExceptionCode]
			,[Minutes]
			,[RecordInsertTimestamp]
        )
        VALUES
        (
            DATETIMEFROMPARTS(SUBSTRING(S.[Date], 5, 4), SUBSTRING(S.[Date], 1, 2), SUBSTRING(S.[Date], 3, 2), '0', '0', '0', '0')
			,S.[MuID]
			,S.[LogonID]
			,RIGHT(S.[AgentName], (LEN(S.[AgentName])-CHARINDEX(', ', S.[AgentName])-1))
			,SUBSTRING(S.[AgentName], CHARINDEX('#', S.[AgentName])+1, (CHARINDEX(', ', S.[AgentName])-CHARINDEX('#', S.[AgentName])-1))
			,LEFT(S.[AgentName], CHARINDEX('#', S.[AgentName]) - 1)
			,S.[ExceptionCode]
			,S.[Minutes]
			,S.[RecordInsertTimestamp]
        )
    ;
	SELECT * FROM #invalidRecords;
GO


--procedure to truncate staging table
CREATE PROCEDURE [dbo].[sp_truncateStaging_AgentScheduleSummary]
WITH EXECUTE AS OWNER
AS
BEGIN
	TRUNCATE TABLE [dbo].[staging_IEX_AgentScheduleSummary];
END
GO



--grant permissions

-- required to truncate the table
GRANT ALTER ON OBJECT:: [dbo].[staging_IEX_AgentScheduleSummary] TO [id-carsrosteringservice-npe];

--execute Sp
GRANT EXECUTE ON OBJECT::[dbo].[sp_truncateStaging_AgentScheduleSummary] TO [id-carsrosteringservice-npe];
GRANT EXECUTE ON OBJECT::[dbo].[sp_commit_AgentScheduleSummary] TO [id-carsrosteringservice-npe];