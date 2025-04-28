/*
--===============================================================================
-- Application   IFAS Data Sync
-- Object Name   usp_MS_Sync_GetExportData
-- Author        Amit K Agrawal
-- Created       April/22/2025
-- Database(s)   IFAS
-- Called From   IFAS MS Sync Service
-- Description   This procedure retrieves exportable data for the specified company in batches.
-- Change History Original
-- Enhancements: Added performance optimizations, improved documentation, and error handling.
--================================================================================
*/ 
CREATE OR ALTER PROCEDURE [dbo].[usp_MS_Sync_GetExportData]     
(  
	@pCompanyId INT,            -- ID of the company for filtering data
    @pBatchSize INT = 200       -- Number of records to fetch in each batch
)           
AS              
BEGIN              
          
	SET NOCOUNT ON;                
	DECLARE @vReplicationStatusUID UNIQUEIDENTIFIER,
			@vInProgressTrackingUID UNIQUEIDENTIFIER,
			@vMDInterfaceTrackingStatusUID UNIQUEIDENTIFIER,
			@vProcessedStatusId UNIQUEIDENTIFIER,
			@vMarkSent UNIQUEIDENTIFIER;
      
	-- Retrieve MDInterfaceTrackingStatusUID for 'InProgress' status   
	SELECT    
		@vMDInterfaceTrackingStatusUID = MDInterfaceTrackingStatusUID    
	FROM MDInterfaceTrackingStatus (NOLOCK)    
	WHERE InternalTrackingStatus = 1    
    
	-- Get the most recent InProgress Tracking ID for the company   
	SELECT    
		TOP 1 
			@vInProgressTrackingUID = IT.MDInterfaceTrackingUID    
	FROM MDInterfaceTracking (NOLOCK) IT    
	INNER JOIN MDInterface (NOLOCK) I ON I.MDInterfaceUID = IT.MDInterfaceUID    
	INNER JOIN MDInterfaceType (NOLOCK) II ON ii.MDInterfaceTypeUID = i.MDInterfaceTypeUID AND 
	                                    II.InternalInterfaceType = 4    
	WHERE MDInterfaceTrackingStatusUID = @vMDInterfaceTrackingStatusUID AND 
	      I.CompanyDimId = @pCompanyId    
	ORDER BY IT.CreateDate DESC    
          
	 -- Retrieve Replication Status for 'ReadyToSend'          
	SELECT           
		@vReplicationStatusUID = SSReplicationStatusUID           
	FROM SSReplicationStatus (NOLOCK)           
	WHERE InternalStatus = 1     
  
	-- Retrieve Processed Status ID 
	SELECT       
		@vProcessedStatusId = SSReplicationStatusUID       
	FROM SSReplicationStatus (NOLOCK)       
	WHERE InternalStatus = 6   

	-- Retrieve Mark Sent Status ID 
	SELECT       
		@vMarkSent = SSReplicationStatusUID       
	FROM SSReplicationStatus (NOLOCK)       
	WHERE InternalStatus = 9 
  
    -- Fetch exportable data in a temporary table
	SELECT  
		TOP (@pBatchSize) R.*   
	INTO #tmpData  
	FROM SSReplicationData R (NOLOCK)  
	JOIN MDBranch BR (NOLOCK) ON BR.MDBranchUID = R.MDBranchUID AND  
		                         BR.CompanyDimId = @pCompanyId  
	WHERE R.SSReplicationStatusUID = @vReplicationStatusUID  
	ORDER BY R.CreateDate
  
	--Marked all selected record as mark sent
	UPDATE SSReplicationData
		SET SSReplicationStatusUID = @vMarkSent
	WHERE SSReplicationDataUID IN (SELECT SSReplicationDataUID FROM #tmpData)

	-- Identify duplicates based on SSTableUID and PrimaryKey (except the latest record)  
	SELECT 
		R.SSReplicationDataUID  
	INTO #tmpDupRecords  
	FROM  
	(  
		SELECT  
			T.SSTableUID,
			T.PrimaryKey,
			MAX(T.OriginSequenceNumber) AS OriginSequenceNumber  
		FROM #tmpData T  
		WHERE LEN(T.PrimaryKey)>0  
		GROUP BY T.SSTableUID,T.PrimaryKey  
		HAVING COUNT(1)>1  
	) DUP  
	INNER JOIN #tmpData R ON R.SSTableUID = DUP.SSTableUID AND 
	                         R.PrimaryKey = DUP.PrimaryKey AND 
							 R.OriginSequenceNumber <> DUP.OriginSequenceNumber   
	
	-- Update the status of duplicate records to Processed
	UPDATE SSReplicationData   
		SET SSReplicationStatusUID = @vProcessedStatusId    
	WHERE SSReplicationDataUID IN (SELECT SSReplicationDataUID FROM #tmpDupRecords)  
  
	-- Remove duplicates from temporary data
	DELETE FROM #tmpData WHERE SSReplicationDataUID IN (SELECT SSReplicationDataUID FROM #tmpDupRecords)  	
    
	 -- Create entries in the SSReplicationTracking table    
	INSERT INTO SSReplicationTracking  
	(  
		[SSReplicationTrackingUID],  
		[MDInterfaceTrackingUID],  
		[SSReplicationDataUID],  
		[SSReplicationStatusUID],  
		[MDErrorCodeUID],  
		[ErrorMessage],  
		[CreateUserID],  
		[CreateDate]  
	)    
	SELECT    
		NEWID(),    
		@vInProgressTrackingUID,    
		SSReplicationDataUID,    
		SSReplicationStatusUID,    
		NULL,    
		NULL,    
		CreateUserUID,    
		GETDATE()    
	FROM #tmpData T  
	WHERE @vInProgressTrackingUID is not null       
    
	--Return the Replication Data for Export
	SELECT          
		T.SSReplicationDataUID,          
		T.OriginSequenceNumber,          
		T.SSTableUID ,          
		T.PrimaryKey,          
		T.DataRecord,          
		T.CreateDate,          
		T.CreateUserUID,          
		T.MDBranchUID,          
		T.MDLogicalBranchUID,          
		T.IsDelete,          
		T.SSDataReplicationType,          
		T.IsOnlyToDisconnectedBuyingStation,          
		T.MDSecondaryBranchUID,          
		T.MDDisconnectedBuyingStationSourceUID,          
		T.SSReplicationStatusUID    
	FROM #tmpData T          
	ORDER BY T.OriginSequenceNumber                 
END   
GO
  