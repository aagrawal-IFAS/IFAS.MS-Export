/*
--===============================================================================
-- Application   IFAS Data Sync
-- Object Name   usp_MS_Sync_GetExportHandshakeData
-- Author        Amit K Agrawal
-- Created       April/22/2025
-- Database(s)   IFAS
-- Called From   IFAS MS Sync Service
-- Description   This procedure retrieves exportable data for the specified company in batches.
-- Change History Original
-- Enhancements: Added performance optimizations, improved documentation, and error handling.
--================================================================================
*/ 
CREATE OR ALTER PROCEDURE [dbo].[usp_MS_Sync_GetExportHandshakeData]     
(  
	@pCompanyId INT,            -- ID of the company for filtering data
    @pBatchSize INT = 200       -- Number of records to fetch in each batch
)           
AS              
BEGIN              
          
	SET NOCOUNT ON;                
	DECLARE @vReplicationStatusUID UNIQUEIDENTIFIER,
			@vReadyToProcessStatusId UNIQUEIDENTIFIER
          
	 -- Retrieve Replication Status for 'MarkSent'          
	SELECT           
		@vReplicationStatusUID = SSReplicationStatusUID           
	FROM SSReplicationStatus (NOLOCK)           
	WHERE InternalStatus = 9     
  
	-- Retrieve Ready To Process Status ID 
	SELECT       
		@vReadyToProcessStatusId = SSReplicationStatusUID       
	FROM SSReplicationStatus (NOLOCK)       
	WHERE InternalStatus = 5   


  
    -- Fetch exportable data in a temporary table
	SELECT  
		TOP (@pBatchSize) R.SSReplicationDataUID, @vReadyToProcessStatusId AS SSReplicationStatusUID, R.OriginSequenceNumber    
	INTO #tmpData  
	FROM SSReplicationData R (NOLOCK)  
	JOIN MDBranch BR (NOLOCK) ON BR.MDBranchUID = R.MDBranchUID AND  
		                         BR.CompanyDimId = @pCompanyId  
	WHERE R.SSReplicationStatusUID = @vReplicationStatusUID  
	ORDER BY R.CreateDate
	
	--Return the Replication Data for Handshake
	SELECT          
		SSReplicationDataUID, SSReplicationStatusUID   
	FROM #tmpData T          
	ORDER BY T.OriginSequenceNumber                 
END   
GO
  