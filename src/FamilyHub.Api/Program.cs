using FamilyHub.Api.Chores;
using FamilyHub.Api.Data;
using FamilyHub.Api.Expiry;
using FamilyHub.Api.FirstAid;
using FamilyHub.Api.Households;
using FamilyHub.Api.Medications;
using FamilyHub.Api.Notifications;
using FamilyHub.Api.Paperless;
using FamilyHub.Api.Settings;
using FamilyHub.Api.Warranties;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDataProtection();
builder.Services.AddHttpClient<IPaperlessDocumentClient, PaperlessDocumentClient>();

builder.Services.AddDbContext<FamilyHubDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FamilyHub")));
builder.Services.AddScoped<MedicationReminderDispatcher>();
builder.Services.AddScoped<LowStockReminderDispatcher>();
builder.Services.AddSingleton<IPushNotificationSender, WebPushNotificationSender>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<MedicationReminderWorker>();
builder.Services.AddHostedService<LowStockReminderWorker>();

var authenticationBuilder = builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // For API responses (especially WASM clients), return 403 Forbidden instead of redirecting
        // The client will handle the 403 and redirect to login as needed
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    // Only registered when real credentials are configured: Google's handler implements
    // IAuthenticationRequestHandler, so ASP.NET Core validates its options on every request
    // (to check the callback path) even when it isn't the default scheme.
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}
builder.Services.AddAuthorization();

var app = builder.Build();

// No SDK/dotnet-ef in the published container, so apply migrations here on startup.
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<FamilyHubDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/login", (string? returnUrl) => Results.Challenge(
    new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
    [GoogleDefaults.AuthenticationScheme]));

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

app.MapHouseholdEndpoints();
app.MapChoreEndpoints();
app.MapExpiryEndpoints();
app.MapWarrantyEndpoints();
app.MapFirstAidEndpoints();
app.MapMedicationEndpoints();
app.MapPushSubscriptionEndpoints();
app.MapPaperlessSettingsEndpoints();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

