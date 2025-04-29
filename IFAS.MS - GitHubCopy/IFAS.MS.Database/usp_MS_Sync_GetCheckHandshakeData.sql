/*
--===============================================================================
-- Application   IFAS Data Sync
-- Object Name   usp_MS_Sync_GetCheckHandshakeData
-- Author        Amit K Agrawal
-- Created       April/22/2025
-- Database(s)   IFAS
-- Called From   IFAS MS Sync Service
-- Description   This procedure retrieves exportable data for the specified company in batches.
-- Change History Original
-- Enhancements: Added performance optimizations, improved documentation, and error handling.
--================================================================================
*/ 
CREATE OR ALTER PROCEDURE [dbo].[usp_MS_Sync_GetCheckHandshakeData]     
(  
	@pExportHandShakeData NVARCHAR(MAX)
)           
AS              
BEGIN              
          
	SET NOCOUNT ON;                
	DECLARE @vReadyToProcessStatusId UNIQUEIDENTIFIER,
			@vReplicationStatusUID UNIQUEIDENTIFIER,
			@vReadyToSendStatusUID UNIQUEIDENTIFIER;

	-- Validate if the input is a valid JSON
	IF(ISJSON(@pExportHandShakeData)=0)  
	BEGIN  
		RAISERROR('Invalid JSON',16,1)    
	END  
          
	 -- Retrieve Replication Status for 'Sent'          
	SELECT           
		@vReplicationStatusUID = SSReplicationStatusUID           
	FROM SSReplicationStatus (NOLOCK)           
	WHERE InternalStatus = 2     

	 -- Retrieve Replication Status for 'Ready To Send'          
	SELECT           
		@vReadyToProcessStatusId = SSReplicationStatusUID           
	FROM SSReplicationStatus (NOLOCK)           
	WHERE InternalStatus = 1    
  
	-- Retrieve Ready To Process Status ID 
	SELECT       
		@vReadyToProcessStatusId = SSReplicationStatusUID       
	FROM SSReplicationStatus (NOLOCK)       
	WHERE InternalStatus = 5   

	SELECT 
		* 
	INTO #tmpData  
	FROM OPENJSON(@pExportHandShakeData)  
	WITH  
	(  
		SSReplicationDataUID UNIQUEIDENTIFIER,  			
		SSReplicationStatusUID UNIQUEIDENTIFIER  
	)  
	WHERE SSReplicationDataUID <>'00000000-0000-0000-0000-000000000000'

	IF EXISTS(SELECT 1 FROM #tmpData)
	BEGIN
		SELECT
			T.SSReplicationDataUID,
			CASE 
				WHEN R.SSReplicationDataUID IS NULL 
					THEN @vReadyToProcessStatusId 
				ELSE
					CASE WHEN R.SSReplicationStatusUID = @vReadyToProcessStatusId 
						THEN @vReplicationStatusUID 
					ELSE 
						R.SSReplicationStatusUID 
					END 
			END AS SSReplicationStatusUID
		FROM #tmpData T
		LEFT JOIN SSReplicationData R (NOLOCK) ON R.SSReplicationDataUID = T.SSReplicationDataUID
	END
END   
GO
  