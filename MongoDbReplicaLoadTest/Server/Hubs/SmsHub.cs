using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDbReplicaLoadTest.Server.Services;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MongoDbReplicaLoadTest.Server.Hubs;

public class SmsHub : Hub
{
    private readonly ILogger<SmsHub> logger;
    private readonly IMongoDb db;
    private CacheService cache;

    public SmsHub(ILogger<SmsHub> logger, IMongoDb db, CacheService cache)
    {
        this.logger = logger;
        this.db = db;
        this.cache = cache;
    }

    public async Task<ActionResult<long>> CountCollectionRowAsync()
    {
        return await db.CountQueueCollectionRowAsync();
    }

    public async Task<ActionResult<PostResponse>> InsertSmsAsync([FromBody] SmsBase sms)
    {
        return await db.InsertSmsToQueueAsync(sms.From, sms.To, sms.Content);
    }

    public async Task<ActionResult<PostResponse>> InsertBatchSmsAsync([FromBody] InsertMultiSms sms)
    {
        return await db.InsertBatchSmsAsync(sms.Iteration, sms.From, sms.To, sms.Content);
    }

    public async Task<ActionResult<Sms>> GetSmsAsync([FromQuery] string msgId)
    {
        return await db.GetSmsFromQueueAsync(msgId);
    }

    public async IAsyncEnumerable<Sms> StreamSmsAsync(int delayMs, [EnumeratorCancellation] CancellationToken ct)
    {
        cache.DelayMsForSmsStreaming = delayMs;
        var list = db.StreamSmsAsync(ct);

        await foreach (var l in list)
        {
            yield return l;

            try
            {
                await Task.Delay(cache.DelayMsForSmsStreaming, ct);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation($"Stop SMS streaming requested by {Context.UserIdentifier} on {DateTime.Now}");
                yield break;
            }
        }
    }

    public PostResponse SetDelayForSmsStream(int delayMs)
    {
        cache.DelayMsForSmsStreaming = delayMs;
        return new PostResponse
        {
            IsSuccess = true,
            Message = $"Delay set to {cache.DelayMsForSmsStreaming} ms"
        };
    }

    public override Task OnConnectedAsync()
    {
        var cid = Context.ConnectionId;
        logger.LogInformation($"{nameof(OnConnectedAsync)} = ConnectionId: {cid}, UserId: {Context.UserIdentifier}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }
}
