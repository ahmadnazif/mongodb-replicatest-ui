using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDbReplicaLoadTest.Shared.Enums;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MongoDbReplicaLoadTest.Server.Services;

public class MongoDb : IMongoDb
{
    public const string COLLECTION_QUEUE = "queue";
    private readonly ILogger<MongoDb> logger;
    private readonly IMongoDatabase db;
    private readonly Dictionary<string, IMongoCollection<Sms>> collections = new();
    public MongoUrl MongoUrl { get; }

    public MongoDb(ILogger<MongoDb> logger, IConfiguration config)
    {
        try
        {
            this.logger = logger;

            MongoUrl = GetMongoUrl(config);
            MongoClient client = new(MongoUrl);

            db = client.GetDatabase(MongoUrl.DatabaseName);
            collections.Add(COLLECTION_QUEUE, db.GetCollection<Sms>(COLLECTION_QUEUE));
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }        
    }

    private static MongoUrl GetMongoUrl(IConfiguration config)
    {
        List<MongoServerAddress> servers = new();
        foreach (var s in config.GetSection("MongoDb:Servers").Get<string[]>().ToList())
        {
            servers.Add(new MongoServerAddress(s));
        }

        MongoUrlBuilder builder = new()
        {
            Servers = servers,
            DatabaseName = config["MongoDb:DbName"]
        };

        var useReplica = bool.Parse(config["MongoDb:Replication:UseReplication"]);
        if (useReplica)
            builder.ReplicaSetName = config["MongoDb:Replication:ReplicaSetName"];

        return builder.ToMongoUrl();
    }

    private IMongoCollection<Sms> QueueCollection => collections[COLLECTION_QUEUE];

    public object GetSettings(MongoSettingsType type)
    {
        return type switch
        {
            MongoSettingsType.Db => db.Settings,
            MongoSettingsType.Client => db.Client.Settings,
            MongoSettingsType.QueueCollection => QueueCollection.Settings,
            _ => null
        };
    }

    public async Task<PostResponse> PingServerAsync()
    {
        try
        {
            var result = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            return new PostResponse
            {
                IsSuccess = true,
                Message = $"MongoDb Server '{MongoUrl.Url}' connected. Checked on {DateTime.Now.ToLongTimeString()}"
            };
        }
        catch (Exception ex)
        {
            return new PostResponse
            {
                IsSuccess = false,
                Message = $"Failed to connect to server '{MongoUrl.Url}: {ex.Message}"
            };
        }
    }

    private static FilterDefinition<Sms> EqFilter(string msgId) => Builders<Sms>.Filter.Eq(x => x.MsgId, msgId);

    public async Task<long> CountQueueCollectionRowAsync()
    {
        try
        {
            return await QueueCollection.EstimatedDocumentCountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return 0;
        }
    }

    public async Task<PostResponse> InsertSmsToQueueAsync(string from, string to, string content)
    {
        try
        {
            var msgId = Guid.NewGuid().ToString("N").ToUpper();
            Sms sms = new()
            {
                MsgId = msgId,
                From = from,
                To = to,
                Content = content,
                InTime = DateTime.Now
            };

            await QueueCollection.InsertOneAsync(sms);
            return new PostResponse
            {
                IsSuccess = true,
                Message = msgId
            };
        }
        catch (Exception ex)
        {
            return new PostResponse
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public async Task<Sms> GetSmsFromQueueAsync(string msgId)
    {
        var filter = EqFilter(msgId);
        return await QueueCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<PostResponse> InsertBatchSmsAsync(int iteration, string from, string to, string content)
    {
        try
        {
            List<Sms> smsList = new();
            for (int i = 0; i < iteration; i++)
            {
                smsList.Add(new Sms
                {
                    MsgId = Guid.NewGuid().ToString("N").ToUpper(),
                    Content = content,
                    From = from,
                    To = to,
                    InTime = DateTime.Now
                });
            }

            Stopwatch sw = Stopwatch.StartNew();
            await QueueCollection.InsertManyAsync(smsList);
            sw.Stop();

            return new PostResponse
            {
                IsSuccess = true,
                Message = $"{iteration} inserted [{sw.Elapsed}]"
            };
        }
        catch (Exception ex)
        {
            return new PostResponse
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public async IAsyncEnumerable<Sms> StreamSmsAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var filter = Builders<Sms>.Filter.Empty;
        var list = QueueCollection.Find(filter).ToAsyncEnumerable(ct);

        await foreach (var l in list)
        {
            if (ct.IsCancellationRequested)
                yield break;

            yield return new Sms
            {
                MsgId = l.MsgId,
                From = l.From,
                To = l.To,
                InTime = l.InTime,
                Content = l.Content
            };
        }
    }
}

public interface IMongoDb
{
    MongoUrl MongoUrl { get; }
    object GetSettings(MongoSettingsType type);
    Task<PostResponse> PingServerAsync();
    Task<long> CountQueueCollectionRowAsync();
    Task<PostResponse> InsertSmsToQueueAsync(string from, string to, string content);
    Task<PostResponse> InsertBatchSmsAsync(int iteration, string from, string to, string content);
    Task<Sms> GetSmsFromQueueAsync(string msgId);
    IAsyncEnumerable<Sms> StreamSmsAsync(CancellationToken ct);
}
