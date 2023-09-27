using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MongoDbReplicaLoadTest.Server.Services;

public class MongoDb : IMongoDb
{
    public const string COLL_QUEUE = "queue";
    private readonly ILogger<MongoDb> logger;
    private readonly Dictionary<string, IMongoCollection<Sms>> collections = new();

    public MongoDb(ILogger<MongoDb> logger, IConfiguration config)
    {
        this.logger = logger;

        var host = config["MongoDb:Host"];
        var port = int.Parse(config["MongoDb:Port"]);
        var dbName = config["MongoDb:DbName"];
        var conString = $"mongodb://{host}:{port}";

        MongoClient client = new(conString);
        var db = client.GetDatabase(dbName);

        collections.Add(COLL_QUEUE, db.GetCollection<Sms>(COLL_QUEUE));
    }

    private IMongoCollection<Sms> QueueCollection => collections[COLL_QUEUE];

    private static FilterDefinition<Sms> EqFilter(string msgId) => Builders<Sms>.Filter.Eq(x => x.MsgId, msgId);

    public async Task<long> CountQueueCollectionRowAsync() => await QueueCollection.EstimatedDocumentCountAsync();

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
    Task<long> CountQueueCollectionRowAsync();
    Task<PostResponse> InsertSmsToQueueAsync(string from, string to, string content);
    Task<PostResponse> InsertBatchSmsAsync(int iteration, string from, string to, string content);
    Task<Sms> GetSmsFromQueueAsync(string msgId);
    IAsyncEnumerable<Sms> StreamSmsAsync(CancellationToken ct);
}
