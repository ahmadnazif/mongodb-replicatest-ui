using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDbReplicaLoadTest.Server.Services;
using MongoDbReplicaLoadTest.Shared.Enums;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MongoDbReplicaLoadTest.Server.Hubs;

public class MongoHub : Hub
{
    private readonly ILogger<MongoHub> logger;
    private readonly IMongoDb db;
    private readonly CacheService cache;

    public MongoHub(ILogger<MongoHub> logger, IMongoDb db, CacheService cache)
    {
        this.logger = logger;
        this.db = db;
        this.cache = cache;
    }

    public string GetMongoUrl()
    {
        try
        {
            var settings = db.MongoUrl;
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public string GetSettings(MongoSettingsType type)
    {
        try
        {
            var settings = db.GetSettings(type);
            return JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<PostResponse> PingServerAsync()
    {
        return await db.PingServerAsync();
    }

    public async Task<long> CountCollectionRowAsync()
    {
        return await db.CountQueueCollectionRowAsync();
    }

    public async Task<PostResponse> InsertAsync(SmsBase sms)
    {
        return await db.InsertSmsToQueueAsync(sms.From, sms.To, sms.Content);
    }

    public async Task<PostResponse> InsertBatchAsync(InsertMultiSms sms)
    {
        return await db.InsertBatchSmsAsync(sms.Iteration, sms.From, sms.To, sms.Content);
    }

    public async Task<Sms> GetAsync(string msgId)
    {
        return await db.GetSmsFromQueueAsync(msgId);
    }

    public async IAsyncEnumerable<Sms> StreamAsync(int delayMs, [EnumeratorCancellation] CancellationToken ct)
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

    public PostResponse SetDelay(int delayMs)
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
