using FamilyHub.Api.Data;
using FamilyHub.Api.Notifications;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FamilyHub.Api.IntegrationTests;

/// <summary>
/// Boots the real API against a real, disposable Postgres container (Testcontainers) and swaps
/// in <see cref="TestAuthHandler"/> so tests authenticate without hitting Google.
/// </summary>
public class FamilyHubApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:PollingEnabled"] = "false",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FamilyHubDbContext>>();
            services.AddDbContext<FamilyHubDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
            services.RemoveAll<IPushNotificationSender>();
            services.AddSingleton<FakePushNotificationSender>();
            services.AddSingleton<IPushNotificationSender>(provider =>
                provider.GetRequiredService<FakePushNotificationSender>());

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // PostConfigure always runs last, so this wins over the app's own Cookie/Google defaults
            // regardless of registration order, making every request authenticate via TestAuthHandler.
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                options.DefaultSignInScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FamilyHubDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
