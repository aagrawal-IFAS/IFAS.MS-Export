/*
--===============================================================================
-- Application   IFAS Data Sync
-- Object Name   usp_MS_Sync_UpdateCheckHandshakeData
-- Author        Amit K Agrawal
-- Created       April/22/2025
-- Database(s)   IFAS
-- Called From   IFAS MS Sync Service
-- Description   This procedure retrieves exportable data for the specified company in batches.
-- Change History Original
-- Enhancements: Added performance optimizations, improved documentation, and error handling.
--================================================================================
*/ 
CREATE OR ALTER PROCEDURE [dbo].[usp_MS_Sync_UpdateCheckHandshakeData]     
(  
	@pExportHandShakeData NVARCHAR(MAX)
)           
AS              
BEGIN              
          
	SET NOCOUNT ON;                
	
	-- Validate if the input is a valid JSON
	IF(ISJSON(@pExportHandShakeData)=0)  
	BEGIN  
		RAISERROR('Invalid JSON',16,1)    
	END  

	BEGIN TRANSACTION
	BEGIN TRY          	
		SELECT 
			* 
		INTO #tmpData  
		FROM OPENJSON(@pExportHandShakeData)  
		WITH  
		(  
			ssReplicationDataUID UNIQUEIDENTIFIER,  			
			ssReplicationStatusUID UNIQUEIDENTIFIER  
		)  
		WHERE SSReplicationDataUID <>'00000000-0000-0000-0000-000000000000'

		IF EXISTS(SELECT 1 FROM #tmpData)
		BEGIN
			UPDATE R
				SET R.SSReplicationStatusUID = T.SSReplicationStatusUID
			FROM SSReplicationData R
			JOIN #tmpData T ON T.ssReplicationDataUID = R.SSReplicationDataUID
		END
		COMMIT TRANSACTION
	END TRY
	BEGIN CATCH
		ROLLBACK TRANSACTION
	END CATCH
END   
GO
  