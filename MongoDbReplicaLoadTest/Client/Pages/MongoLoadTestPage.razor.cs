using Microsoft.AspNetCore.Components;
using MongoDbReplicaLoadTest.Client.HubClients;
using Sotsera.Blazor.Toaster;
using System.Text.Json;

namespace MongoDbReplicaLoadTest.Client.Pages;

public class MongoLoadTestBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected ILogger<MongoLoadTestBase> Logger { get; set; }
    [Inject] protected SmsClient Signalr { get; set; }
    [Inject] protected IToaster Toastr { get; set; }

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

            await Task.Delay(100);
            StateHasChanged();
        };
    }

    #region Part 1
    protected MarkupString Result1 { get; set; } = new();
    protected async Task CountAllSmsAsync()
    {
        var count = await Signalr.CountAsync();

        if (count > 0)
            Result1 = new($"{count} SMS exist in 'queue' collection");
        else
        {
            Result1 = new();
            Toastr.Warning($"NO SMS exist");
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
            Result2 = new(JsonSerializer.Serialize(sms));
        else
        {
            Result2 = new();
            Toastr.Warning($"SMS with msgid '{MsgId}' not exist");
        }
    }

    protected void Clear2() => Result2 = new();

    #endregion


    protected async Task StartAsync()
    {
        HandleSignalrEvent();
        await Signalr.StartAsync();
        ConnectionInfo = new($"<b>ConnectionId:</b> {Signalr.ConnectionId}, <b>Connected on</b> {Signalr.ConnectedTime}");
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
