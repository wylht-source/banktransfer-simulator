namespace BankingApi.Domain.Enums;

public enum AiAnalysisStatus
{
    NotRequested = 0,
    Pending      = 1,   // message published — awaiting Python service
    Processing   = 2,   // Python confirmed receipt (future)
    Completed    = 3,   // result received (future)
    Failed       = 4    // failed to publish message to Service Bus
}