using FamilyHub;
using FamilyHub.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<HouseholdClient>();
builder.Services.AddScoped<ChoreClient>();
builder.Services.AddScoped<ExpiryClient>();
builder.Services.AddScoped<WarrantyClient>();
builder.Services.AddScoped<FirstAidClient>();
builder.Services.AddScoped<MedicationClient>();
builder.Services.AddScoped<PushNotificationClient>();

await builder.Build().RunAsync();
