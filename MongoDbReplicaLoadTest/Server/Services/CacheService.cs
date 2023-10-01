namespace MongoDbReplicaLoadTest.Server.Services;

public class CacheService
{
    public int DelayMsForSmsStreaming { get; set; } = 1000;
    public int DelayMsForInsertMultiSms { get; set; } = 1000;

}
