using Microsoft.AspNetCore.Mvc;
using MongoDbReplicaLoadTest.Server.Services;
using MongoDbReplicaLoadTest.Shared.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MongoDbReplicaLoadTest.Server.Controllers;

[Route("api/mongo")]
[ApiController]
public class MongoController : ControllerBase
{
    private ILogger<MongoController> logger;
    private IMongoDb db;

    public MongoController(IMongoDb db, ILogger<MongoController> logger)
    {
        this.db = db;
        this.logger = logger;
    }

    [HttpGet("queue/count-row")]
    public async Task<ActionResult<long>> CountQueueRow()
    {
        return await db.CountQueueCollectionRowAsync();
    }

    [HttpPost("queue/insert-one-sms")]
    public async Task<ActionResult<PostResponse>> InsertSmsToQueue([FromBody] SmsBase sms)
    {
        return await db.InsertSmsToQueueAsync(sms.From, sms.To, sms.Content);
    }

    [HttpPost("queue/insert-multi-sms")]
    public async Task<ActionResult<PostResponse>> InsertSmsToQueue([FromBody] InsertMultiSms sms)
    {
        return await db.InsertBatchSmsAsync(sms.Iteration, sms.From, sms.To, sms.Content);
    }

    [HttpGet("queue/get-sms")]
    public async Task<ActionResult<Sms>> GetSms([FromQuery] string msgId)
    {
        return await db.GetSmsFromQueueAsync(msgId);
    }

    [HttpGet("queue/stream-all-sms-console")]
    public async Task<ActionResult<PostResponse>> StreamSmsConsole(CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        var list = db.StreamSmsAsync(ct);

        int i = 0;
        await foreach (var l in list)
        {
            i++;
            logger.LogInformation($"[{i}] {JsonSerializer.Serialize(l)}");
        }
        sw.Stop();

        return new PostResponse
        {
            IsSuccess = true,
            Message = sw.Elapsed.ToString()
        };
    }

    [HttpGet("queue/stream-all-sms")]
    public async IAsyncEnumerable<Sms> StreamSms([EnumeratorCancellation] CancellationToken ct)
    {
        var list = db.StreamSmsAsync(ct);

        int i = 0;
        await foreach (var l in list)
        {
            i++;
            logger.LogInformation($"[{i}] {JsonSerializer.Serialize(l)}");
            yield return new Sms
            {
                MsgId = l.MsgId,
                From = l.From,
                To = l.To,
                Content = l.Content,
                InTime = l.InTime
            };
        }
    }
}
