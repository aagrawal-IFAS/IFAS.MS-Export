namespace IFAS.MS.Common
{
    public enum ReplicationStatus
    {
        ReadyToSend = 1,
        Sent = 2,
        Synchronized = 3,
        SentError = 4,
        ReadyToProcess = 5,
        Processed = 6,
        ReceiveError = 7,
        ProcessError = 8,
        MarkSent = 9
    }
}
