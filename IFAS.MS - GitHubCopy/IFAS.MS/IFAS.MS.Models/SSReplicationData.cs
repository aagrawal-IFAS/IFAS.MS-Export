namespace IFAS.MS.Models
{
    public class SSReplicationData
    {
        public Guid SSReplicationDataUID { get; set; }
        public Int64 OriginSequenceNumber { get; set; }
        public Guid SSTableUID { get; set; }
        public required string PrimaryKey { get; set; }
        public required string DataRecord { get; set; }
        public DateTime CreateDate { get; set; }
        public int CreateUserUID { get; set; }
        public Guid MDBranchUID { get; set; }
        public Guid MDLogicalBranchUID { get; set; }
        public bool IsDelete { get; set; }
        public Guid SSDataReplicationType {  get; set; }
        public bool IsOnlyToDisconnectedBuyingStation { get; set; }
        public Guid MDSecondaryBranchUID { get; set; }
        public Guid MDDisconnectedBuyingStationSourceUID {  get; set; }
        public Guid SSReplicationStatusUID { get; set; }

        public SSReplicationData()
        {
            
        }
    }
}
