drop table if exists [dbo].[staging_IEX_ActiveForecast];
drop table if exists [dbo].[IEX_ActiveForecast];

drop procedure if exists [dbo].[sp_truncateStaging_ActiveForecast];	
drop procedure if exists [dbo].[sp_commit_ActiveForecast];

--tables 

create table [dbo].[staging_IEX_ActiveForecast](
	[id] INT IDENTITY(1,1) PRIMARY KEY,	
	[Date] [varchar](16) NOT NULL,
	[Period] [varchar](8) NOT NULL,
	[SAGroupID] [varchar](8) NULL,
	[SAGRoupName] [varchar](64) NULL,
	[CTID] [varchar](8) NULL,
	[CTName] [varchar](64) NULL,
	[FcstContactsReceived] [decimal](8, 2) NULL,
	[FcstContactsHandled] [decimal](8, 2) NULL,
	[FcstAHT] [decimal](8, 2) NULL,
	[FcstOcc] [tinyint] NULL,
	[FcstReq] [decimal](8, 2) NULL,
	[RevPlanReq] [decimal](8, 2) NULL,
	[CommitPlanReq] [decimal](8, 2) NULL,
	[SchedOpen] [decimal](8, 2) NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[staging_IEX_ActiveForecast] ADD  CONSTRAINT [df_adf_iex_activeforecast_RecordInsertTimestamp]  DEFAULT (getutcdate()) FOR [RecordInsertTimestamp]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_ActiveForecast](
	[Date] [datetime] NOT NULL,
	[Period] [time](7) NOT NULL,
	[SAGroupID] [varchar](8) NULL,
	[SAGRoupName] [varchar](64) NULL,
	[CTID] [varchar](8) NULL,
	[CTName] [varchar](64) NULL,
	[FcstContactsReceived] [decimal](8, 2) NULL,
	[FcstContactsHandled] [decimal](8, 2) NULL,
	[FcstAHT] [decimal](8, 2) NULL,
	[FcstOcc] [tinyint] NULL,
	[FcstReq] [decimal](8, 2) NULL,
	[RevPlanReq] [decimal](8, 2) NULL,
	[CommitPlanReq] [decimal](8, 2) NULL,
	[SchedOpen] [decimal](8, 2) NULL,
	[RecordInsertTimestamp] [datetime2](7) NOT NULL,
	[RecordInsertTimestampWST]  AS ([dbo].[ConvertUTC2WST]([RecordInsertTimestamp]))
) ON [PRIMARY]
GO


--truncate SP
--procedure to truncate staging table
CREATE PROCEDURE [dbo].[sp_truncateStaging_ActiveForecast]
WITH EXECUTE AS OWNER
AS
BEGIN
	TRUNCATE TABLE [dbo].[staging_IEX_ActiveForecast];
END
GO


SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[sp_commit_ActiveForecast]
AS
    SET NOCOUNT ON
    ;

	DECLARE @ApplicableSAGroupIDs TABLE (saGroupID varchar(8));
	INSERT INTO @ApplicableSAGroupIDs VALUES ('4000'), ('4001'), ('4002');

    -- Merge to base table
    MERGE [dbo].[IEX_ActiveForecast] AS D
    USING [dbo].[staging_IEX_ActiveForecast] AS S
    ON
		D.[Date] = DATETIMEFROMPARTS(SUBSTRING(S.[Date], 5, 4), SUBSTRING(S.[Date], 1, 2), SUBSTRING(S.[Date], 3, 2), '0', '0', '0', '0')
		AND D.[Period] = CONVERT(TIME, S.[Period])
		AND D.[CTID] = S.[CTID]
    WHEN MATCHED
		AND S.[SAGroupID] IN (SELECT saGroupID FROM @ApplicableSaGroupIDs)
	THEN
        UPDATE SET
			D.[SAGroupID] = S.[SAGroupID]
			,D.[SAGRoupName] = S.[SAGRoupName]
			,D.[CTName] = S.[CTName]
			,D.[FcstContactsReceived] = S.[FcstContactsReceived]
			,D.[FcstContactsHandled] = S.[FcstContactsHandled]
			,D.[FcstAHT] = S.[FcstAHT]
			,D.[FcstOcc] = S.[FcstOcc]
			,D.[FcstReq] = S.[FcstReq]
			,D.[RevPlanReq] = S.[RevPlanReq]
			,D.[CommitPlanReq] = S.[CommitPlanReq]
			,D.[SchedOpen] = S.[SchedOpen]
			,D.[RecordInsertTimestamp] = S.[RecordInsertTimestamp]

    WHEN NOT MATCHED BY TARGET
		AND S.[SAGroupID] IN (SELECT saGroupID FROM @ApplicableSaGroupIDs)
        THEN
        INSERT
        (
            [Date]
			,[Period]
			,[SAGroupID]
			,[SAGRoupName]
			,[CTID]
			,[CTName]
			,[FcstContactsReceived]
			,[FcstContactsHandled]
			,[FcstAHT]
			,[FcstOcc]
			,[FcstReq]
			,[RevPlanReq]
			,[CommitPlanReq]
			,[SchedOpen]
			,[RecordInsertTimestamp]
        )
        VALUES
        (
            DATETIMEFROMPARTS(SUBSTRING(S.[Date], 5, 4), SUBSTRING(S.[Date], 1, 2), SUBSTRING(S.[Date], 3, 2), '0', '0', '0', '0')
			,CONVERT(TIME, S.[Period])
			,S.[SAGroupID]
			,S.[SAGRoupName]
			,S.[CTID]
			,S.[CTName]
			,S.[FcstContactsReceived]
			,S.[FcstContactsHandled]
			,S.[FcstAHT]
			,S.[FcstOcc]
			,S.[FcstReq]
			,S.[RevPlanReq]
			,S.[CommitPlanReq]
			,S.[SchedOpen]
			,S.[RecordInsertTimestamp]
        )
    ;
GO