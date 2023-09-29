using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using MongoDbReplicaLoadTest.Shared.Enums;
using MongoDbReplicaLoadTest.Shared.Models;

namespace MongoDbReplicaLoadTest.Client.HubClients;

public class MongoClient : IAsyncDisposable
{
    private const string HUB_ROUTE = "/mongo-hub";
    private string clientId = "BlazorClient";
    private readonly ILogger<MongoClient> logger;
    private readonly HubConnection hubConnection;
    private CancellationTokenSource cts;
    public bool IsConnected { get; private set; }
    public string ConnectionId { get; private set; }
    public DateTime? ConnectedTime { get; private set; }
    public string HubUrl { get; }

    public MongoClient(ILogger<MongoClient> logger, NavigationManager navMan)
    {
        this.logger = logger;
        HubUrl = $"{navMan.BaseUri.TrimEnd('/')}{HUB_ROUTE}?clientid={clientId}";
        hubConnection = new HubConnectionBuilder().WithUrl(HubUrl).WithAutomaticReconnect().AddMessagePackProtocol().Build();
    }

    #region OnReceived
    public class ReceivedEventArgs : EventArgs
    {
        public string Param { get; set; }
        public ReceivedEventArgs(string param) => Param = param;
    }

    public delegate void ReceivedEventHandler(object sender, ReceivedEventArgs e);
    public event ReceivedEventHandler OnReceived;
    #endregion

    #region OnReconnecting
    public class DisconnectedEventArgs : EventArgs
    {
        public Exception? Ex { get; set; }
        public DisconnectedEventArgs(Exception? exception) => Ex = exception;
    }

    public delegate void ReconnectingEventHandler(object sender, DisconnectedEventArgs e);
    public event ReconnectingEventHandler OnReconnecting;
    #endregion

    #region OnReconnected
    public class ReconnectedEventArgs : EventArgs
    {
        public string ConnectionId { get; set; }
        public ReconnectedEventArgs(string connectionId) => ConnectionId = connectionId;
    }
    public delegate void ReconnectedEventHandler(object sender, ReconnectedEventArgs e);
    public event ReconnectedEventHandler OnReconnected;
    #endregion

    #region OnClosed
    public class ClosedEventArgs : EventArgs
    {
        public Exception? Ex { get; set; }
        public ClosedEventArgs(Exception? ex) => Ex = ex;
    }

    public delegate void ClosedEventHandler(object sender, ClosedEventArgs e);
    public event ClosedEventHandler OnClosed;
    #endregion

    public async Task StartAsync()
    {
        try
        {
            HandleServerEvents();

            hubConnection.On<string>("Receive", (x) =>
            {
                OnReceived?.Invoke(this, new ReceivedEventArgs(x));
            });

            await hubConnection.StartAsync();

            IsConnected = true;
            ConnectionId = hubConnection.ConnectionId;
            ConnectedTime = DateTime.Now;

            logger.LogInformation($"Hub connected. Connection ID: {ConnectionId}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }
    }

    private void HandleServerEvents()
    {
        hubConnection.Closed += (e) =>
        {
            OnClosed?.Invoke(this, new ClosedEventArgs(e));

            IsConnected = false;
            logger.LogInformation($"Hub: {hubConnection.State}, Error: {e}");
            return Task.CompletedTask;
        };

        hubConnection.Reconnecting += (e) =>
        {
            OnReconnecting?.Invoke(this, new DisconnectedEventArgs(e));

            IsConnected = false;
            logger.LogInformation($"Hub: {hubConnection.State}, Error: {e}");
            return Task.CompletedTask;
        };

        hubConnection.Reconnected += (cid) =>
        {
            OnReconnected?.Invoke(this, new ReconnectedEventArgs(cid));

            IsConnected = true;
            ConnectionId = cid;
            ConnectedTime = DateTime.Now;
            logger.LogInformation($"Hub: {hubConnection.State}, ConnectionId: {cid}");
            return Task.CompletedTask;
        };
    }

    #region Methods

    public async Task<string> GetMongoUrlAsync()
    {
        try
        {
            return await hubConnection.InvokeAsync<string>("GetMongoUrl");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return ex.Message;
        }
    }

    public async Task<string> GetSettingsAsync(MongoSettingsType type)
    {
        try
        {
            return await hubConnection.InvokeAsync<string>("GetSettings", type);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return ex.Message;
        }
    }

    public async Task<PostResponse> PingServerAsync()
    {
        try
        {
            return await hubConnection.InvokeAsync<PostResponse>("PingServerAsync");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return new PostResponse
            {
                IsSuccess = false,
                Message = ex.Message
            };
        }
    }

    public async Task<PostResponse> InsertAsync(SmsBase sms)
    {
        try
        {
            return await hubConnection.InvokeAsync<PostResponse>("InsertAsync", sms);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return new PostResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<PostResponse> InsertBatchAsync(InsertMultiSms sms)
    {
        try
        {
            return await hubConnection.InvokeAsync<PostResponse>("InsertBatchAsync", sms);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return new PostResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<long> CountAsync()
    {
        try
        {
            return await hubConnection.InvokeAsync<long>("CountCollectionRowAsync");
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return 0;
        }
    }

    public async Task<Sms> GetAsync(string msgId)
    {
        try
        {
            return await hubConnection.InvokeAsync<Sms>("GetAsync", msgId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return null;
        }
    }

    public async IAsyncEnumerable<Sms> StartStreaming(int delayMs)
    {
        cts = new();
        var list = hubConnection.StreamAsync<Sms>("StreamAsync", delayMs, cts.Token);

        await foreach (var l in list)
        {
            if (cts.Token.IsCancellationRequested)
                yield break;

            yield return l;
        }
    }

    public void StopStreaming() => cts.Cancel();

    public async Task<PostResponse> SetDelayAsync(int delayMs)
    {
        return await hubConnection.InvokeAsync<PostResponse>("SetDelay", delayMs);
    }

    #endregion

    public async Task StopAsync()
    {
        try
        {
            await hubConnection.StopAsync();
            IsConnected = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
        }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
