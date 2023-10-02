using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Servers;
using MongoDB.Driver.Linq;
using MongoDbReplicaLoadTest.Shared.Enums;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks.Sources;

namespace MongoDbReplicaLoadTest.Server.Services;

public class MongoDb : IMongoDb
{
    private readonly ILogger<MongoDb> logger;

    private readonly MongoClient client;
    private readonly IMongoDatabase db;
    private readonly IMongoDatabase adminDb;
    private readonly IMongoCollection<Sms> queueCollection;

    private string primaryServerIp;
    public MongoUrl MongoUrl { get; }

    public MongoDb(ILogger<MongoDb> logger, IConfiguration config)
    {
        try
        {
            this.logger = logger;

            MongoUrl = GetMongoUrl(config);
            client = new(MongoUrl);

            db = client.GetDatabase(MongoUrl.DatabaseName);
            adminDb = client.GetDatabase("admin");
            queueCollection = db.GetCollection<Sms>("queue");

            client.Cluster.DescriptionChanged += (s, e) =>
            {
                var primary = e.NewClusterDescription.Servers.Where(d => d.Type == ServerType.ReplicaSetPrimary).SingleOrDefault();
                primaryServerIp = primary.EndPoint.ToString();
            };
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

    public object GetSettings(MongoSettingsType type)
    {
        return type switch
        {
            MongoSettingsType.Db => db.Settings,
            MongoSettingsType.Client => db.Client.Settings,
            MongoSettingsType.QueueCollection => queueCollection.Settings,
            MongoSettingsType.Cluster => client.Cluster.Settings,
            _ => null
        };
    }

    public async Task<object> GetReplicaInfoAsync()
    {
        try
        {
            var isMasterResult = await adminDb.RunCommandAsync<BsonDocument>(new BsonDocument { { "isMaster", 1 } });
            if (isMasterResult.Contains("setName"))
            {
                var members = client.Cluster.Description.Servers
                    .Select(server => new
                    {
                        ReplicaSetName = server.ReplicaSetConfig.Name,
                        Host = server.EndPoint.ToString(),
                        State = server.State.ToString(),
                        Type = server.Type.ToString(),
                        server.HelloOk,
                        server.IsCompatibleWithDriver,
                        server.IsDataBearing,
                        server.LogicalSessionTimeout,
                        server.MaxBatchCount,
                        server.MaxDocumentSize,
                        server.MaxMessageSize,
                        server.MaxWireDocumentSize,
                        server.MaxWireVersion,
                        server.ReasonChanged,
                        server.LastHeartbeatTimestamp,
                        server.AverageRoundTripTime,
                        server.ElectionId,
                        server.HeartbeatInterval,
                        server.ServerId
                    });

                return members;
            }
            else
                return new List<string>();
        }
        catch (Exception)
        {
            return new List<string>();
        }
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
            return await queueCollection.EstimatedDocumentCountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return 0;
        }
    }

    public async Task<PostResponse> InsertOneSmsAsync(string from, string to, string content)
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

            await queueCollection.InsertOneAsync(sms);
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
        return await queueCollection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<PostResponse> InsertManySmsAsync(int iteration, string from, string to, string content)
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
            await queueCollection.InsertManyAsync(smsList);
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
        var list = queueCollection.Find(filter).ToAsyncEnumerable(ct);

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
    Task<object> GetReplicaInfoAsync();
    Task<PostResponse> PingServerAsync();
    Task<long> CountQueueCollectionRowAsync();
    Task<PostResponse> InsertOneSmsAsync(string from, string to, string content);
    Task<PostResponse> InsertManySmsAsync(int iteration, string from, string to, string content);
    Task<Sms> GetSmsFromQueueAsync(string msgId);
    IAsyncEnumerable<Sms> StreamSmsAsync(CancellationToken ct);
}
