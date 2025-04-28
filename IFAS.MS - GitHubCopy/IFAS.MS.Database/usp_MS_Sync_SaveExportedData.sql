/*
--===============================================================================
-- Application:  IFAS
-- Object Name:  usp_MS_Sync_SaveExportedData
-- Author:       Amit Kumar Agrawal
-- Created:      April/12/2025
-- Database(s):  IFAS
-- Called From:  IFAS -> Sync Micro Service
-- Description:  This procedure saves the exported data into the central repository.
-- Change History: Original
-- Enhancements: Added better error handling, logging for auditing, and comments for clarification.
--================================================================================
*/     
CREATE OR ALTER Procedure [dbo].[usp_MS_Sync_SaveExportedData]     
(  
	@pExportedData NVARCHAR(MAX)  -- JSON string of data to be processed
)   
AS        
BEGIN   
  
	SET NOCOUNT ON;  
  
	DECLARE @vErrorMessage NVARCHAR(MAX) = '', 
	        @vReturnValue INT,
			@vReadyToProcessId UNIQUEIDENTIFIER,  
			@vMDBranchUID UNIQUEIDENTIFIER = null,  
			@vMDDisconnectedBuyingStationUID UNIQUEIDENTIFIER = null,  
			@vMDInterfaceUID UNIQUEIDENTIFIER,  
			@vMDInterfaceTrackingStatusUID UNIQUEIDENTIFIER,  
			@vMDInterfaceTrackingUID UNIQUEIDENTIFIER = NEWID();  
  
	-- Validate if the input is a valid JSON
	IF(ISJSON(@pExportedData)=0)  
	BEGIN  
		SELECT @vReturnValue=0, @vErrorMessage='Invalid JSON';   
		GOTO ErrorHandler   
	END  
	
	-- Fetch the Ready to Process status ID
	SELECT 
		@vReadyToProcessId= SSReplicationStatusUID 
	FROM  SSReplicationStatus (NOLOCK) 
	WHERE InternalStatus=5; --Ready to Process  
  
    -- Fetch the MDInterfaceUID for Synchronization type
	SELECT 
		@vMDInterfaceUID= MDInterfaceUID 
	FROM MDInterfacetype IT (NOLOCK)  
	INNER JOIN MDInterface I (NOLOCK) ON I.MDInterfaceTypeUID=IT.MDInterfaceTypeUID  
	WHERE IT.InternalInterfaceType=4 --Synchronization  
    
	BEGIN TRANSACTION      
	BEGIN TRY 
		-- Parse the JSON data into a temporary table
		SELECT 
			* 
		INTO #tmpData  
		FROM OPENJSON(@pExportedData)  
		WITH  
		(  
			SSReplicationDataUID UNIQUEIDENTIFIER,  
			OriginSequenceNumber bigint,  
			SSTableUID UNIQUEIDENTIFIER,  
			PrimaryKey NVARCHAR(MAX),  
			DataRecord NVARCHAR(MAX),  
			CreateDate DATETIME2,  
			CreateUserUID INT,  
			MDBranchUID UNIQUEIDENTIFIER,  
			MDLogicalBranchUID UNIQUEIDENTIFIER,  
			IsDelete BIT,  
			SSDataReplicationType UNIQUEIDENTIFIER,  
			IsOnlyToDisconnectedBuyingStation BIT,  
			MDSecondaryBranchUID UNIQUEIDENTIFIER,  
			MDDisconnectedBuyingStationSourceUID UNIQUEIDENTIFIER,  
			SSReplicationStatusUID UNIQUEIDENTIFIER  
		)  
		WHERE SSReplicationDataUID <>'00000000-0000-0000-0000-000000000000'  
  
        -- Retrieve Branch and BuyingStattion UID details from parsed data
        IF EXISTS (SELECT 1 FROM #tmpData)
            SELECT TOP 1
                @vMDBranchUID = MDBranchUID,
                @vMDDisconnectedBuyingStationUID = MDDisconnectedBuyingStationSourceUID
            FROM #tmpData;

		-- Insert valid data into SSReplicationData if not already present
		INSERT INTO SSReplicationData 
		(  
			SSReplicationDataUID,  
			SSTableUID,  
			PrimaryKey,  
			DataRecord,  
			CreateDate,  
			CreateUserUID,  
			MDBranchUID,  
			MDLogicalBranchUID,  
			IsDelete,  
			SSDataReplicationType,  
			IsOnlyToDisconnectedBuyingStation,  
			MDSecondaryBranchUID,  
			MDDisconnectedBuyingStationSourceUID,  
			SSReplicationStatusUID  
		)  
		SELECT 
			T.SSReplicationDataUID,  
			T.SSTableUID,  
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
			@vReadyToProcessId 
		FROM #tmpData T
		LEFT JOIN SSReplicationData D (NOLOCK) ON D.SSReplicationDataUID = T.SSReplicationDataUID
		WHERE D.SSReplicationDataUID IS NULL  
		ORDER BY T.OriginSequenceNumber  
		
		-- Determine tracking status based on data presence
		IF EXISTS(SELECT TOP 1 1 FROM #tmpData)  
			SELECT 
				@vMDInterfaceTrackingStatusUID = MDInterfaceTrackingStatusUID 
			FROM  MDInterfaceTrackingStatus (NOLOCK) 
			WHERE InternalTrackingStatus=2; --Success  
		ELSE  
			SELECT 
				@vMDInterfaceTrackingStatusUID= MDInterfaceTrackingStatusUID 
			FROM  MDInterfaceTrackingStatus (NOLOCK) 
			WHERE InternalTrackingStatus=3; --NODATA  
  
		-- Insert in Interface tracking  
		INSERT INTO MDInterfaceTracking 
		(  
			MDInterfaceTrackingUID,  
			MDInterfaceUID,  
			MDBranchUID,  
			MDDisconnectedBuyingStationUID,  
			MDInterfaceTrackingStatusUID,  
			CreateUserID,  
			CreateDate  
		)  
		VALUES
		(  
			@vMDInterfaceTrackingUID,  
			@vMDInterfaceUID,  
			@vMDBranchUID,  
			@vMDDisconnectedBuyingStationUID,  
			@vMDInterfaceTrackingStatusUID,  
			0,  
			GETDATE()  
		)  
  
		-- Insert in Replication tracking  
		INSERT INTO SSReplicationTracking 
		(  
			SSReplicationTrackingUID,  
			MDInterfaceTrackingUID,  
			SSReplicationDataUID,  
			SSReplicationStatusUID,  
			CreateUserID,  
			CreateDate  
		)  
		SELECT 
			NEWID(),  
			@vMDInterfaceTrackingUID,  
			SSReplicationDataUID,  
			@vReadyToProcessId,  
			0,  
			GETDATE() 
		FROM #tmpData;  
  
		COMMIT TRANSACTION;              
		
		SELECT  
			@vErrorMessage = '',
			@vReturnValue = 1  
		
		GOTO ErrorHandler  
	END TRY        
	BEGIN CATCH        
		ROLLBACK TRANSACTION;    
		
		SELECT 
			@vMDInterfaceTrackingStatusUID = MDInterfaceTrackingStatusUID 
		FROM  MDInterfaceTrackingStatus (NOLOCK) 
		WHERE InternalTrackingStatus=0; --Failed  
    
		SELECT 
			@vErrorMessage = CAST(ERROR_MESSAGE() AS NVARCHAR(MAX)),                                          
			@vReturnValue = 2  

		-- Insert in Interface tracking  
		INSERT INTO MDInterfaceTracking 
		(  
			MDInterfaceTrackingUID,  
			MDInterfaceUID,  
			MDBranchUID,  
			MDDisconnectedBuyingStationUID,  
			MDInterfaceTrackingStatusUID,  
			ErrorMessage,  
			CreateUserID,  
			CreateDate  
		)  
		VALUES
		(  
			@vMDInterfaceTrackingUID,  
			@vMDInterfaceUID,  
			@vMDBranchUID,  
			@vMDDisconnectedBuyingStationUID,  
			@vMDInterfaceTrackingStatusUID,  
			@vErrorMessage,		
			0,  
			GETDATE()  
		)  
		
		GOTO ErrorHandler  
	END CATCH        
   
	RETURN(1);  
  
	ErrorHandler:  
		SELECT 
			@vReturnValue AS ErrorId,
			@vErrorMessage AS ErrorMessage  
  
	RETURN(0)  
END  
GO  