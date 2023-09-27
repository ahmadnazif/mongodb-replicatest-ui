using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MongoDbReplicaLoadTest.Client;
using MongoDbReplicaLoadTest.Client.HubClients;
using Sotsera.Blazor.Toaster.Core.Models;
using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<SmsClient>();

builder.Services.AddToaster(new ToasterConfiguration
{
    PositionClass = Defaults.Classes.Position.TopCenter,
    PreventDuplicates = true,
    NewestOnTop = true,
    ShowCloseIcon = true,
    ShowProgressBar = true,
    VisibleStateDuration = 5000,
    MaximumOpacity = 100,
    ShowTransitionDuration = 100,
    HideTransitionDuration = 100,
    MaxDisplayedToasts = 3
});

builder.Services.AddLoadingBarService();
builder.UseLoadingBar();

//builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
