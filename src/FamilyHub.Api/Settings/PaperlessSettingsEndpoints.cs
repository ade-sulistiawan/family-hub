using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Settings;

public static class PaperlessSettingsEndpoints
{
    private const string ProtectorPurpose = "FamilyHub.Paperless.ApiToken.v1";

    public static RouteGroupBuilder MapPaperlessSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/paperless").RequireAuthorization();
        group.MapGet("/", Get);
        group.MapPut("/", Save);
        return group;
    }

    private static async Task<IResult> Get(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var settings = await db.PaperlessSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.HouseholdId == currentMember.HouseholdId);

        return Results.Ok(new PaperlessSettingsResponse(settings?.BaseUrl, settings is not null));
    }

    private static async Task<IResult> Save(
        SavePaperlessSettingsRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider)
    {
        var baseUrl = request.BaseUrl?.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBaseUrl)
            || (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps)
            || parsedBaseUrl.UserInfo.Length > 0
            || baseUrl!.Length > 2048)
        {
            return Results.BadRequest("Enter a valid Paperless-ngx HTTP or HTTPS URL.");
        }

        var apiToken = request.ApiToken?.Trim();
        if (string.IsNullOrEmpty(apiToken) || apiToken.Length > 512)
        {
            return Results.BadRequest("API token must be between 1 and 512 characters.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var normalizedBaseUrl = parsedBaseUrl.AbsoluteUri.TrimEnd('/') + "/";
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var settings = await db.PaperlessSettings
            .SingleOrDefaultAsync(candidate => candidate.HouseholdId == currentMember.HouseholdId);

        if (settings is null)
        {
            settings = new PaperlessSettings
            {
                HouseholdId = currentMember.HouseholdId,
                BaseUrl = normalizedBaseUrl,
                EncryptedApiToken = protector.Protect(apiToken),
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.PaperlessSettings.Add(settings);
        }
        else
        {
            settings.BaseUrl = normalizedBaseUrl;
            settings.EncryptedApiToken = protector.Protect(apiToken);
            settings.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    internal static string UnprotectApiToken(
        PaperlessSettings settings,
        IDataProtectionProvider dataProtectionProvider) =>
        dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(settings.EncryptedApiToken);
}

public record SavePaperlessSettingsRequest(string BaseUrl, string ApiToken);

public record PaperlessSettingsResponse(string? BaseUrl, bool Configured);