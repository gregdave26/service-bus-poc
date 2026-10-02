--cleanup
drop table if exists [dbo].[IEX_Lineup_History];
drop table if exists [dbo].[IEX_LineupStaging];
drop table if exists [dbo].[IEX_Lineup_Xml];

drop procedure IF EXISTS [dbo].[IEX_Clear_Lineups];
drop procedure if exists [dbo].[IEX_Lineups_Prepare];
drop procedure if exists [dbo].[IEX_Lineups_PrepareXml];
drop procedure if exists [dbo].[IEX_Lineups_PrepareXmlEmpty];
drop procedure if exists [dbo].[IEX_Lineups_UpdateHistory];
drop procedure if exists [dbo].[IEX_LookupLineups];
--tables
--[dbo].[IEX_Lineup_History]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_Lineup_History](
	[LineupName] [varchar](64) NOT NULL,
	[ShiftStart] [datetime2](7) NOT NULL,
	[UserAgency] [varchar](16) NULL,
	[SchedDate] [date] NOT NULL,
	[ShiftDurationMinutes] [int] NOT NULL,
	[ShiftDuration] [int] NOT NULL,
	[NoUnits] [int] NOT NULL
) ON [PRIMARY]
GO

-- [dbo].[IEX_LineupStaging]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_LineupStaging](
	[LineupName] [varchar](64) NOT NULL,
	[ShiftStart] [datetime2](7) NOT NULL,
	[SchedDate] [date] NOT NULL,
	[ExcDate] [date] NOT NULL,
	[ShiftDurationMinutes] [int] NOT NULL,
	[UserAgency] [varchar](16) NULL,
	[UnitID] [varchar](16) NULL,
	[EmployeeID] [varchar](16) NULL,
	[IsEmployee] [varchar](8) NULL,
	[VehicleID] [varchar](16) NULL,
	[AwaySuburb] [varchar](64) NULL,
	[XCoord] [decimal](12, 4) NULL,
	[YCoord] [decimal](12, 4) NULL,
	[ShiftDurationSeconds] [int] NOT NULL,
	[logonID] [varchar](16) NULL
) ON [PRIMARY]
GO

--[dbo].[IEX_Lineup_Xml]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[IEX_Lineup_Xml](
	[LineupName] [varchar](64) NOT NULL,
	[ShiftStart] [datetime2](7) NOT NULL,
	[ShiftDurationMinutes] [int] NOT NULL,
	[UserAgency] [varchar](16) NULL,
	[Xml] [varchar](max) NULL,
	[SchedDate] [date] NULL,
	[ShiftDuration] [int] NULL,
	[NoUnits] [int] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


-- stored procedures
--[dbo].[IEX_Clear_Lineups]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_Clear_Lineups]
WITH EXECUTE AS OWNER
AS
BEGIN
	TRUNCATE TABLE [dbo].[IEX_LineupStaging];
	DELETE FROM [dbo].[IEX_Lineup_History] WHERE ShiftStart < DATEADD(day, -7, GETDATE());
END
GO

--[dbo].[IEX_Lineups_Prepare]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_Lineups_Prepare]
	@LineupStartTime DATETIME,
	@LineupStopTime DATETIME
AS
--DECLARE @LineupStartTime DATETIME = '2023-06-01 00:00:00';
--DECLARE @LineupStopTime DATETIME = '2023-06-02 00:00:00';

DECLARE @ExceptionCode VARCHAR(8) = 'Away';
WITH AwayPatrols AS
	(
		SELECT DISTINCT
			det.IdentifyNumber
			,det.logonID
		FROM dbo.IEX_AgentScheduleDetail det
		WHERE EXISTS
		(
			SELECT *
			FROM dbo.IEX_AgentScheduleDetail detb
			WHERE det.logonID = detb.logonID
			AND det.schedDate = detb.schedDate
			AND det.StartTime = detb.StartTime
			AND detb.ExceptionCode = @ExceptionCode
			AND det.excDate >= @LineupStartTime 
			AND det.excDate < @LineupStopTime
)
	),
	ShiftTimesInDayPreFiltered AS (
		SELECT
		det.*
		,DATEADD(DAY, DATEDIFF(DAY, det.StartTime, det.excDate), CONVERT(DATETIME2, det.StartTime)) AS ShiftStart
		,CASE
		WHEN det.StopTime < det.StartTime -- The shift has crossed midnight
		THEN DATEADD(DAY, DATEDIFF(DAY, det.StopTime, det.excDate) + 1, CONVERT(DATETIME2, det.StopTime))
		ELSE
		DATEADD(DAY, DATEDIFF(DAY, det.StopTime, det.excDate), CONVERT(DATETIME2, det.StopTime))
		END AS ShiftStop
		FROM dbo.IEX_AgentScheduleDetail det
		WHERE det.schedDate >= @LineupStartTime
		AND det.schedDate < @LineupStopTime
		AND det.ExceptionCode NOT IN (
			'Cancelled Casual',
			'Cancelled Overtime',
			'Casual UPA',
			'Community Connection',
			'Emergency Annual Leave',
			'Jury Duty',
			'Long Service Leave',
			'LWOP',
			'Overtime - Digital',
			'Overtime - Training',
			'Overtime Meeting',
			'Overtime QA',
			'Overtime Upskill Trg',
			'Parental Leave',
			'Public Holiday -non working hours',
			'Purchase Leave',
			'Resigned',
			'Unavailable'
		)
	),
	ShiftTimesNotAvailable AS (
		SELECT logonId FROM ShiftTimesInDayPreFiltered x WHERE ExceptionCode IN (
										'NRS Unfulfillment',
										'NRS Unaccepted',
										'Unplanned Leave',
										'Annual Leave',
										'Long Service Leave',
										'Workers Compensation','Workers Comp',
										'Purchase Leave',
										'Parental Leave',
										'Emergency Annual Leave',
										'Covid Leave',
										'Patrol Annual Leave',
										'Patrol Public Holiday',
										'NRS Cancelled Additional'
								) AND (SELECT COUNT(1) FROM ShiftTimesInDayPreFiltered y WHERE y.logonID = x.logonID) = 1
	),
	ShiftTimesInDay AS (
		SELECT * FROM ShiftTimesInDayPreFiltered WHERE logonID NOT IN (SELECT logonID FROM ShiftTimesNotAvailable)
	),
	ContiguousShifts AS (
		SELECT
		sh.LogonID AS LogonID
		,sh.ShiftStart AS ShiftStart
		,sh.ShiftStop AS ShiftStop
		,DATEDIFF(second, StartTime, LAG(StopTime, 1, NULL) OVER (PARTITION BY LogonId ORDER BY ShiftStart)) as DateDifference
		,CASE WHEN COALESCE(DATEDIFF(second, StartTime, LAG(StopTime, 1, NULL) OVER (PARTITION BY LogonId ORDER BY ShiftStart)), -1) < 0 THEN 1 ELSE 0 END as IsNotContiguous
		,ROW_NUMBER() OVER(PARTITION BY LogonId ORDER BY SchedDate, ShiftStart, ShiftStop) as RowNo
		FROM ShiftTimesInDay sh
	),
	ContiguousShifts2 AS
	(
		SELECT
			sh.LogonID AS LogonID
			,sh.ShiftStart AS ShiftStart
			,sh.ShiftStop AS ShiftStop
			,sh.IsNotContiguous
			,Rank() Over(Partition By LogonId ORDER BY IsNotContiguous DESC, DateDifference) as DateRank
		FROM ContiguousShifts sh
	), rankedShifts AS (
		SELECT a.LogonId, a.ShiftStart as ShiftStart, a.ShiftStop as ShiftStop, a.IsNotContiguous + a.DateRank as Rank
		FROM ContiguousShifts2 a LEFT JOIN ContiguousShifts2 b ON a.LogonID = b.LogonID AND a.ShiftStart = b.ShiftStop
	), preMasterShifts AS (
		SELECT LogonId
			, MIN(ShiftStart) as ShiftStart
			, MAX(ShiftStop) as ShiftStop
			FROM rankedShifts GROUP BY LogonId, Rank
	), MasterShifts AS (
		SELECT LogonId
			,ShiftStart
			,ShiftStop
			,DATEDIFF(MINUTE, ShiftStart, ShiftStop) AS ShiftDurationMinutes
			,DATEDIFF(SECOND, ShiftStart, ShiftStop) AS ShiftDurationSeconds
			FROM preMasterShifts
	) 

	INSERT INTO [dbo].[IEX_LineupStaging]
			   ([LineupName]
			   ,[ShiftStart]
			   ,[SchedDate]
			   ,[ExcDate]
			   ,[ShiftDurationMinutes]
			   ,[UserAgency]
			   ,[UnitID]
			   ,[EmployeeID]
			   ,[IsEmployee]
			   ,[VehicleID]
			   ,[AwaySuburb]
			   ,[XCoord]
			   ,[YCoord]
			   ,[ShiftDurationSeconds]
			   ,[logonID])
	SELECT DISTINCT
		CONCAT('RAC_ROADSIDE_', FORMAT(ms.ShiftStart, 'yyyyMMddHHmm'), '_', ms.ShiftDurationMinutes) AS LineUpName
		,DATEADD(HOUR,-8,ms.ShiftStart) AS ShiftStart
		,det.schedDate AS SchedDate
		,det.excDate AS ExcDate
		,ms.ShiftDurationMinutes AS ShiftDurationMinutes
		,'ROADSIDE' AS UserAgency
		,inf.agentDataUnitID AS UnitID
		,inf.agentDataUserID AS EmployeeID
		,'1' AS IsEmployee
		,inf.agentDataVehicleID AS VehicleID
		,CASE WHEN awy.IdentifyNumber IS NOT NULL THEN inf.agentDataAwayLoc ELSE NULL END AS AwaySuburb
		,CASE WHEN awy.IdentifyNumber IS NOT NULL THEN inf.agentDataAwayXCoord ELSE NULL END AS XCoord
		,CASE WHEN awy.IdentifyNumber IS NOT NULL THEN inf.agentDataAwayYCoord ELSE NULL END AS YCoord
		,ms.ShiftDurationSeconds AS ShiftDurationSeconds
		,det.logonID
	FROM dbo.IEX_AgentScheduleDetail det
	LEFT JOIN AwayPatrols awy ON det.logonID = awy.logonID
	INNER JOIN dbo.IEX_AgentInfo inf
		ON det.logonID = inf.logonID
	INNER JOIN MasterShifts ms
		ON DATEADD(DAY, DATEDIFF(DAY, det.StartTime, det.excDate), CONVERT(DATETIME2, det.StartTime)) = ms.ShiftStart
		AND det.logonID = ms.LogonID
	WHERE det.excDate >= @LineupStartTime
	AND  det.excDate < @LineupStopTime
	AND det.ExceptionCode NOT IN (
			'Cancelled Casual',
			'Cancelled Overtime',
			'Casual UPA',
			'Community Connection',
			'Emergency Annual Leave',
			'Jury Duty',
			'Long Service Leave',
			'LWOP',
			'Overtime - Digital',
			'Overtime - Training',
			'Overtime Meeting',
			'Overtime QA',
			'Overtime Upskill Trg',
			'Parental Leave',
			'Public Holiday -non working hours',
			'Purchase Leave',
			'Resigned',
			'Unavailable'
		)
GO

--[dbo].[IEX_Lineups_PrepareXml]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_Lineups_PrepareXml]
WITH EXECUTE AS OWNER
AS 
BEGIN
	TRUNCATE TABLE [dbo].[IEX_Lineup_Xml];

	WITH XMLNAMESPACES ('http://accnational/xmlns/Personnel' as ns0),
	LineupData AS (
		SELECT * FROM [dbo].[IEX_LineupStaging]
	),
	DistinctLineups AS (
		SELECT DISTINCT LineupName, UserAgency, ShiftStart, SchedDate, ShiftDurationMinutes, ShiftDurationSeconds FROM LineupData
	),
	LineupUnitCounts AS (
		SELECT DISTINCT LineupName, UserAgency, ShiftStart, ShiftDurationMinutes, COUNT(VehicleID) as NoUnits FROM LineupData GROUP BY LineupName, UserAgency, ShiftStart, ShiftDurationMinutes
	)
	INSERT INTO [dbo].[IEX_Lineup_Xml] (LineupName, UserAgency, ShiftStart, ShiftDurationMinutes, ShiftDuration, SchedDate, Xml, NoUnits)
	SELECT 
	LineupName, UserAgency, ShiftStart, ShiftDurationMinutes, ShiftDurationSeconds, SchedDate, (
	--TOP 1
	'<?xml version="1.0" encoding="UTF-8"?>' + CAST((
		SELECT
		[UserAgency] as 'ns0:Agency',
		[LineupName] as 'ns0:LineupName',
		CONVERT(varchar(30), [ShiftStart], 127) + 'Z' as 'ns0:ShiftStart',
		[ShiftDurationSeconds] as 'ns0:ShiftDuration',
		(
			SELECT
			UnitID AS [ns0:Unit/ns0:UnitId],
			XCoord AS [ns0:Unit/ns0:StartShiftLocationXCoordinate],
			YCoord AS [ns0:Unit/ns0:StartShiftLocationYCoordinate],
			(
				SELECT
				CASE WHEN EmployeeID IS NULL THEN '' ELSE EmployeeID END AS [ns0:Employee/ns0:EmpId],
				CASE WHEN IsEmployee = 1 THEN 'true' ELSE 'false' END AS [ns0:Employee/ns0:IsPrimaryEmp]
				FROM LineupData z
				WHERE x.LineupName = z.LineupName  AND x.ShiftStart = z.ShiftStart AND y.UnitId = z.UnitID
				FOR XML PATH (''), TYPE
			) as  [ns0:Unit/ns0:People],
			VehicleID AS [ns0:Unit/ns0:CarId]
			FROM LineupData y
			WHERE x.LineupName = y.LineupName AND x.ShiftStart = y.ShiftStart
			FOR XML PATH (''), TYPE
		) AS 'ns0:Units'
		FROM DistinctLineups x
		WHERE x.LineupName = w.LineupName AND x.ShiftStart = w.ShiftStart
		FOR XML PATH ('ns0:Lineup'), ROOT ('ns0:Lineups'), ELEMENTS
	) AS VARCHAR(MAX))
	) 
	AS Xml,
	(SELECT TOP 1 NoUnits FROM LineupUnitCounts x WHERE x.LineupName = w.LineupName AND x.ShiftStart = w.ShiftStart)
	FROM DistinctLineups w
	--WHERE (SELECT TOP 1 NoUnits FROM LineupUnitCounts x WHERE x.LineupName = w.LineupName AND x.ShiftStart = w.ShiftStart) IS NOT NULL;
END
GO

--[dbo].[IEX_Lineups_PrepareXmlEmpty]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_Lineups_PrepareXmlEmpty]
	@LineupStartTime DATETIME,
	@LineupStopTime DATETIME
AS
BEGIN
	--Generate empty lineups
	WITH XMLNAMESPACES ('http://accnational/xmlns/Personnel' as ns0),
	LineupData AS (
		SELECT * FROM [dbo].[IEX_LineupStaging]
	),
	EmptiedLineups AS (
		SELECT x.* 
		FROM [dbo].[IEX_Lineup_History] x
		LEFT JOIN LineupData y ON x.LineupName = y.LineupName and x.ShiftStart = y.ShiftStart and x.UserAgency = y.UserAgency
		WHERE x.schedDate >= @LineupStartTime AND x.schedDate < @LineupStopTime
		AND  x.NoUnits > 0 AND y.LineupName IS NULL
	)
	INSERT INTO [dbo].[IEX_Lineup_Xml] (LineupName, UserAgency, ShiftStart, SchedDate, ShiftDurationMinutes, ShiftDuration, Xml, NoUnits)
	SELECT 
	LineupName, UserAgency, ShiftStart, SchedDate, ShiftDurationMinutes as ShiftDurationMinutes, ShiftDuration, (
	--TOP 1
	'<?xml version="1.0" encoding="UTF-8"?>' + CAST((
		SELECT
		x.[UserAgency] as 'ns0:Agency',
		x.[LineupName] as 'ns0:LineupName',
		CONVERT(varchar(30), x.[ShiftStart], 127) + 'Z' as 'ns0:ShiftStart',
		x.ShiftDuration as 'ns0:ShiftDuration',
		'' as 'ns0:Units'
		FROM EmptiedLineups x
		LEFT JOIN LineupData y ON x.LineupName = y.LineupName and x.ShiftStart = y.ShiftStart and x.UserAgency = y.UserAgency
		WHERE w.LineupName = x.LineupName AND w.ShiftStart = x.ShiftStart
		FOR XML PATH ('ns0:Lineup'), ROOT ('ns0:Lineups'), ELEMENTS
		) AS VARCHAR(MAX))
	) 
	AS Xml,
	0 as NoUnits
	FROM EmptiedLineups w
END;
GO

--[dbo].[IEX_Lineups_UpdateHistory]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_Lineups_UpdateHistory]
AS
BEGIN
	--Update lineup counts
	MERGE [dbo].[IEX_Lineup_History] AS D
    USING 
		(
			SELECT DISTINCT LineupName, ShiftStart, UserAgency, SchedDate, ShiftDurationMinutes, ShiftDuration, NoUnits
			FROM [dbo].[IEX_Lineup_Xml]
		) AS S
    ON
		D.[LineupName] = S.[LineupName] AND D.ShiftStart = S.ShiftStart
    WHEN MATCHED THEN
		UPDATE SET [NoUnits] = S.NoUnits
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (LineupName, ShiftStart, UserAgency, SchedDate, ShiftDurationMinutes, ShiftDuration, NoUnits) VALUES (s.LineupName, s.ShiftStart, s.UserAgency, s.SchedDate, s.ShiftDurationMinutes, s.ShiftDuration, s.NoUnits)
	;
END
GO

--[dbo].[IEX_LookupLineups]
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[IEX_LookupLineups]
(
    @StartAfter datetime2(7)
)
AS
BEGIN

	SET NOCOUNT ON
    SELECT DISTINCT [LineupName]
      ,[UserAgency]
      ,[ShiftStart]
      ,[ShiftDurationMinutes]
	  ,[XML]
  FROM [dbo].[IEX_Lineup_Xml]
	WHERE [ShiftStart] > @StartAfter
END
GO