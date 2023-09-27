using Microsoft.AspNetCore.SignalR;

namespace MongoDbReplicaLoadTest.Server.Services;

public class SignalRUserIdentifier: IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.GetHttpContext().Request.Query["clientid"];
    }
}
