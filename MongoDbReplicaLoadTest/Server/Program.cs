using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using MongoDbReplicaLoadTest.Server.Hubs;
using MongoDbReplicaLoadTest.Server.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<IMongoDb, MongoDb>();
builder.Services.AddSingleton<IUserIdProvider, SignalRUserIdentifier>();
builder.Services.AddSignalR().AddMessagePackProtocol();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddCors(x => x.AddDefaultPolicy(y => y.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.WebHost.ConfigureKestrel(x =>
{
    var port = int.Parse(config["Port"]);
    x.ListenAnyIP(port);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapHub<MongoHub>("/mongo-hub");

app.MapFallbackToFile("index.html");

await app.RunAsync();
