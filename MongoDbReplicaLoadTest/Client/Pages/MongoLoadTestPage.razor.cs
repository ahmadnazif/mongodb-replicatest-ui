using Microsoft.AspNetCore.Components;
using MongoDbReplicaLoadTest.Client.HubClients;
using MongoDbReplicaLoadTest.Shared.Enums;
using MongoDbReplicaLoadTest.Shared.Models;
using Sotsera.Blazor.Toaster;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MongoDbReplicaLoadTest.Client.Pages;

public class MongoLoadTestBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected ILogger<MongoLoadTestBase> Logger { get; set; }
    [Inject] protected MongoClient Signalr { get; set; }
    [Inject] protected IToaster Toastr { get; set; }
    protected PostResponse MongoPingStatus { get; set; } = new() { IsSuccess = false, Message = "Unknown ping status" };

    protected MarkupString ConnectionInfo { get; set; } = new("Not connected to hub");

    private void HandleSignalrEvent()
    {
        Signalr.OnClosed += (s, e) =>
        {
            if (e.Ex != null)
                Toastr.Error($"Closed on server on {DateTime.Now} with exception");

            ConnectionInfo = new("Connection closed on server");
            StateHasChanged();
        };

        Signalr.OnReconnecting += async (s, e) =>
        {
            if (e.Ex == null)
                Toastr.Error($"Disconnected on {DateTime.Now}. Reconnecting..");
            else
                Toastr.Error($"Disconnected on {DateTime.Now} with exception. Reconnecting..");

            ConnectionInfo = new("Reconnecting..");

            await Task.Delay(100);
            StateHasChanged();
        };

        Signalr.OnReconnected += async (s, e) =>
        {
            Toastr.Success($"Reconnected success. Connection ID: {e.ConnectionId}");
            ConnectionInfo = new($"<b>ConnectionId:</b> {Signalr.ConnectionId}, <b>Connected on</b> {Signalr.ConnectedTime}");

            await PingMongoServerAsync();
            await Task.Delay(100);
            StateHasChanged();
        };
    }

    #region Db settings
    protected MarkupString MongoSettings { get; set; } = new();
    protected async Task GetMongoSettingsAsync(MongoSettingsType type)
    {
        var resp = await Signalr.GetSettingsAsync(type);
        MongoSettings = new($"<strong>{type}:</strong><br />{resp}");
    }

    protected async Task GetMongoUrlAsync()
    {
        var resp = await Signalr.GetMongoUrlAsync();
        MongoSettings = new($"<strong>Mongo URL:</strong><br />{resp}");
    }

    protected async Task PingMongoServerAsync()
    {
        MongoPingStatus = await Signalr.PingServerAsync();
        MongoSettings = new($"<strong>Ping response:</strong><br />{MongoPingStatus.Message}");
    }

    protected void ClearMongoSettings() => MongoSettings = new();

    #endregion

    #region Part 1
    protected MarkupString Result1 { get; set; } = new();
    protected async Task CountAllSmsAsync()
    {
        var count = await Signalr.CountAsync();

        if (count > 0)
            Result1 = new($"{count} SMS exist in 'queue' collection");
        else
        {
            Result1 = new($"No SMS exist in 'queue' collection");
            //Toastr.Warning($"No SMS exist");
        }
    }

    protected void Clear1() => Result1 = new();
    #endregion

    #region Part 2
    protected string MsgId { get; set; }
    protected MarkupString Result2 { get; set; } = new();
    protected async Task CheckSmsExistAsync()
    {
        if (string.IsNullOrWhiteSpace(MsgId))
        {
            Toastr.Warning("Please enter message ID!");
            return;
        }

        var sms = await Signalr.GetAsync(MsgId);

        if (sms != null)
            Result2 = new(JsonSerializer.Serialize(sms, new JsonSerializerOptions { WriteIndented = true}));
        else
        {
            Result2 = new();
            Toastr.Warning($"SMS with msgid '{MsgId}' not exist");
        }
    }

    protected void Clear2() => Result2 = new();

    #endregion

    #region Part 3
    protected bool IsStreamingStarted { get; set; }
    protected int DelayMs { get; set; } = 1000;
    protected long StreamTotalCount { get; set; } = 0;
    protected int StreamCurrentCount { get; set; } = 0;
    protected double StreamPerc { get; set; } = 0;

    protected int StreamCurrentCountInSec { get; set; } = 0;
    protected int Tps { get; set; }
    private Timer TpsTimer { get; set; } = null;

    protected async Task StartStreamingAsync()
    {
        StartTpsTimer();
        IsStreamingStarted = true;
        StreamCurrentCount = 0;
        StreamTotalCount = await Signalr.CountAsync();

        await foreach (var s in Signalr.StartStreaming(DelayMs))
        {
            try
            {
                StreamCurrentCount += 1;
                StreamCurrentCountInSec += 1;
                StreamPerc = (double)StreamCurrentCount / StreamTotalCount * 100;
                Logger.LogInformation($"[{StreamCurrentCount}] MsgID: {s.MsgId}, InTime: {s.InTime}");
                //StateHasChanged();
            }
            catch (OperationCanceledException)
            {
                Toastr.Info($"Stream stopped at {DateTime.Now}");
            }
        }
    }

    private void StartTpsTimer()
    {
        if (TpsTimer != null)
            TpsTimer.Start();
        else
        {
            TpsTimer = new()
            {
                Interval = 2000
            };

            TpsTimer.Start();
            TpsTimer.Elapsed += (s, e) =>
            {
                Tps = StreamCurrentCountInSec / ((int)TpsTimer.Interval / 1000);
                StreamCurrentCountInSec = 0;
                StateHasChanged();
            };
        }
    }

    protected async Task SetStreamingDelayAsync()
    {
        var resp = await Signalr.SetDelayAsync(DelayMs);

        if (resp.IsSuccess)
            Toastr.Success(resp.Message);
        else
            Toastr.Error(resp.Message);
    }

    protected void StopStreaming()
    {
        Signalr.StopStreaming();
        IsStreamingStarted = false;
        TpsTimer.Stop();
        Tps = 0;
    }

    #endregion

    #region Part 4

    protected MarkupString Part4 { get; set; } = new();
    protected InsertMultiSms InsertSmsModel { get; set; } = new();

    public async Task InsertSmsAsync()
    {
        var m = InsertSmsModel;

        var resp = await Signalr.InsertBatchAsync(m);
        Part4 = new(resp.Message);
    }

    #endregion

    protected async Task StartAsync()
    {
        HandleSignalrEvent();
        await Signalr.StartAsync();
        ConnectionInfo = new($"<b>ConnectionId:</b> {Signalr.ConnectionId}, <b>Connected on</b> {Signalr.ConnectedTime}");
        await Task.Run(async () => await PingMongoServerAsync());
    }

    protected async Task StopAsync()
    {
        await Signalr.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
