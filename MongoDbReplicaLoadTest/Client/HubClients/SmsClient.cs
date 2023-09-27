using Microsoft.AspNetCore.SignalR.Client;

namespace MongoDbReplicaLoadTest.Client.HubClients;

public class SmsClient : IAsyncDisposable
{
    private const string HUB_ROUTE = "/sms-hub";
    private string clientId = "BlazorClient";
    private readonly ILogger<SmsClient> logger;
    private readonly HubConnection hubConnection;
    private CancellationTokenSource cts;
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
