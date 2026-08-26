using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using FamilyHub.Api.Items;
using FamilyHub.Api.Paperless;
using FamilyHub.Api.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;

namespace FamilyHub.Api.Warranties;

public static class WarrantyEndpoints
{
    public static RouteGroupBuilder MapWarrantyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/warranty-items").RequireAuthorization();
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPost("/with-document", CreateWithDocument).DisableAntiforgery();
        group.MapPut("/{itemId:guid}", Update);
        group.MapPost("/{itemId:guid}/document", ReplaceDocument).DisableAntiforgery();
        group.MapDelete("/{itemId:guid}", Delete);
        group.MapGet("/{itemId:guid}/document/thumbnail", GetDocumentThumbnail);
        group.MapGet("/{itemId:guid}/document/preview", GetDocumentPreview);
        return group;
    }

    private static async Task<IResult> GetAll(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var items = await (
            from item in db.Items.AsNoTracking()
            join warranty in db.WarrantyFacets on item.Id equals warranty.ItemId
            where item.HouseholdId == currentMember.HouseholdId
            orderby warranty.WarrantyExpiresOn, item.Name
            select new WarrantyItemResponse(
                item.Id,
                item.Name,
                warranty.PurchasedOn,
                warranty.WarrantyExpiresOn,
                warranty.DocumentExternalId))
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> Create(
        CreateWarrantyItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.WarrantyExpiresOn < request.PurchasedOn)
        {
            return Results.BadRequest("Warranty end date cannot be before the purchase date.");
        }

        var documentExternalId = request.DocumentExternalId?.Trim();
        if (documentExternalId?.Length > 200)
        {
            return Results.BadRequest("Document Reference cannot exceed 200 characters.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var item = new Item
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMember.HouseholdId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var warranty = new WarrantyFacet
        {
            ItemId = item.Id,
            PurchasedOn = request.PurchasedOn,
            WarrantyExpiresOn = request.WarrantyExpiresOn,
            DocumentExternalId = string.IsNullOrEmpty(documentExternalId) ? null : documentExternalId,
        };

        db.Items.Add(item);
        db.WarrantyFacets.Add(warranty);
        await db.SaveChangesAsync();

        return Results.Created($"/api/warranty-items/{item.Id}", new WarrantyItemResponse(
            item.Id,
            item.Name,
            warranty.PurchasedOn,
            warranty.WarrantyExpiresOn,
            warranty.DocumentExternalId));
    }

    private static async Task<IResult> CreateWithDocument(
        HttpRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IPaperlessDocumentClient paperlessClient,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Submit warranty details as multipart form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var name = form["name"].ToString().Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (!TryParseOptionalDate(form["purchasedOn"], out var purchasedOn)
            || !TryParseOptionalDate(form["warrantyExpiresOn"], out var warrantyExpiresOn))
        {
            return Results.BadRequest("Purchase and warranty dates must use YYYY-MM-DD.");
        }

        if (warrantyExpiresOn < purchasedOn)
        {
            return Results.BadRequest("Warranty end date cannot be before the purchase date.");
        }

        var document = form.Files.GetFile("document");
        if (document is null || document.Length == 0)
        {
            return Results.BadRequest("Choose a warranty or receipt image.");
        }

        if (document.Length > 10 * 1024 * 1024)
        {
            return Results.BadRequest("The image cannot exceed 10 MB.");
        }

        if (!document.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Only image files can be uploaded.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var settings = await db.PaperlessSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.HouseholdId == currentMember.HouseholdId,
                cancellationToken);
        if (settings is null)
        {
            return Results.Conflict("Configure Paperless-ngx in Settings before uploading a document.");
        }

        string apiToken;
        try
        {
            apiToken = PaperlessSettingsEndpoints.UnprotectApiToken(settings, dataProtectionProvider);
        }
        catch (CryptographicException)
        {
            return Results.Conflict("Save the Paperless-ngx API token again in Settings.");
        }

        string documentExternalId;
        try
        {
            await using var documentStream = document.OpenReadStream();
            documentExternalId = await paperlessClient.UploadAsync(
                new PaperlessConnection(new Uri(settings.BaseUrl), apiToken),
                documentStream,
                Path.GetFileName(document.FileName),
                document.ContentType,
                name,
                cancellationToken);
        }
        catch (PaperlessUploadException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Paperless-ngx upload failed");
        }

        var item = new Item
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMember.HouseholdId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var warranty = new WarrantyFacet
        {
            ItemId = item.Id,
            PurchasedOn = purchasedOn,
            WarrantyExpiresOn = warrantyExpiresOn,
            DocumentExternalId = documentExternalId,
        };

        db.Items.Add(item);
        db.WarrantyFacets.Add(warranty);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/warranty-items/{item.Id}", new WarrantyItemResponse(
            item.Id,
            item.Name,
            warranty.PurchasedOn,
            warranty.WarrantyExpiresOn,
            warranty.DocumentExternalId));
    }

    private static async Task<IResult> Update(
        Guid itemId,
        UpdateWarrantyItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.WarrantyExpiresOn < request.PurchasedOn)
        {
            return Results.BadRequest("Warranty end date cannot be before the purchase date.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var item = await db.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.HouseholdId == currentMember.HouseholdId,
            cancellationToken);
        var warranty = await db.WarrantyFacets.SingleOrDefaultAsync(
            candidate => candidate.ItemId == itemId,
            cancellationToken);
        if (item is null || warranty is null)
        {
            return Results.NotFound();
        }

        item.Name = name;
        warranty.PurchasedOn = request.PurchasedOn;
        warranty.WarrantyExpiresOn = request.WarrantyExpiresOn;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new WarrantyItemResponse(
            item.Id,
            item.Name,
            warranty.PurchasedOn,
            warranty.WarrantyExpiresOn,
            warranty.DocumentExternalId));
    }

    private static async Task<IResult> ReplaceDocument(
        Guid itemId,
        HttpRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IPaperlessDocumentClient paperlessClient,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Submit the replacement document as multipart form data.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var document = form.Files.GetFile("document");
        if (document is null || document.Length == 0)
        {
            return Results.BadRequest("Choose a warranty or receipt image.");
        }

        if (document.Length > 10 * 1024 * 1024)
        {
            return Results.BadRequest("The image cannot exceed 10 MB.");
        }

        if (!document.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest("Only image files can be uploaded.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var item = await db.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.HouseholdId == currentMember.HouseholdId,
            cancellationToken);
        var warranty = await db.WarrantyFacets.SingleOrDefaultAsync(
            candidate => candidate.ItemId == itemId,
            cancellationToken);
        if (item is null || warranty is null)
        {
            return Results.NotFound();
        }

        var (connection, connectionError) = await ResolvePaperlessConnectionAsync(
            currentMember.HouseholdId,
            db,
            dataProtectionProvider,
            cancellationToken);
        if (connectionError is not null)
        {
            return connectionError;
        }

        string documentExternalId;
        try
        {
            await using var documentStream = document.OpenReadStream();
            documentExternalId = await paperlessClient.UploadAsync(
                connection!,
                documentStream,
                Path.GetFileName(document.FileName),
                document.ContentType,
                item.Name,
                cancellationToken);
        }
        catch (PaperlessUploadException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Paperless-ngx upload failed");
        }

        // The previously referenced document is deliberately left in Paperless-ngx; Family Hub
        // only owns the reference, never the file's lifecycle (see ADR-0002).
        warranty.DocumentExternalId = documentExternalId;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new WarrantyItemResponse(
            item.Id,
            item.Name,
            warranty.PurchasedOn,
            warranty.WarrantyExpiresOn,
            warranty.DocumentExternalId));
    }

    private static async Task<IResult> Delete(
        Guid itemId,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        CancellationToken cancellationToken)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        return await ItemOwnership.DeleteAsync(itemId, currentMember.HouseholdId, db, cancellationToken);
    }

    private static async Task<(PaperlessConnection? Connection, IResult? Error)> ResolvePaperlessConnectionAsync(
        Guid householdId,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        CancellationToken cancellationToken)
    {
        var settings = await db.PaperlessSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.HouseholdId == householdId, cancellationToken);
        if (settings is null)
        {
            return (null, Results.Conflict("Configure Paperless-ngx in Settings before uploading a document."));
        }

        try
        {
            var apiToken = PaperlessSettingsEndpoints.UnprotectApiToken(settings, dataProtectionProvider);
            return (new PaperlessConnection(new Uri(settings.BaseUrl), apiToken), null);
        }
        catch (CryptographicException)
        {
            return (null, Results.Conflict("Save the Paperless-ngx API token again in Settings."));
        }
    }

    private static Task<IResult> GetDocumentThumbnail(
        Guid itemId,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IPaperlessDocumentClient paperlessClient,
        CancellationToken cancellationToken) =>
        GetDocumentContent(
            itemId,
            user,
            db,
            dataProtectionProvider,
            paperlessClient.GetThumbnailAsync,
            "Paperless-ngx thumbnail request failed",
            cancellationToken);

    private static Task<IResult> GetDocumentPreview(
        Guid itemId,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        IPaperlessDocumentClient paperlessClient,
        CancellationToken cancellationToken) =>
        GetDocumentContent(
            itemId,
            user,
            db,
            dataProtectionProvider,
            paperlessClient.GetPreviewAsync,
            "Paperless-ngx preview request failed",
            cancellationToken);

    private static async Task<IResult> GetDocumentContent(
        Guid itemId,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        Func<PaperlessConnection, string, CancellationToken, Task<PaperlessThumbnail>> fetchContent,
        string failureTitle,
        CancellationToken cancellationToken)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var documentExternalId = await (
            from item in db.Items.AsNoTracking()
            join warranty in db.WarrantyFacets on item.Id equals warranty.ItemId
            where item.Id == itemId && item.HouseholdId == currentMember.HouseholdId
            select warranty.DocumentExternalId)
            .SingleOrDefaultAsync(cancellationToken);
        if (documentExternalId is null)
        {
            return Results.NotFound();
        }

        var settings = await db.PaperlessSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.HouseholdId == currentMember.HouseholdId,
                cancellationToken);
        if (settings is null)
        {
            return Results.Conflict("Configure Paperless-ngx in Settings before viewing a document.");
        }

        string apiToken;
        try
        {
            apiToken = PaperlessSettingsEndpoints.UnprotectApiToken(settings, dataProtectionProvider);
        }
        catch (CryptographicException)
        {
            return Results.Conflict("Save the Paperless-ngx API token again in Settings.");
        }

        try
        {
            var content = await fetchContent(
                new PaperlessConnection(new Uri(settings.BaseUrl), apiToken),
                documentExternalId,
                cancellationToken);
            return Results.Stream(content.Content, content.ContentType);
        }
        catch (PaperlessUploadException exception)
        {
            return Results.Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: failureTitle);
        }
    }

    private static bool TryParseOptionalDate(string? value, out DateOnly? date)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            date = null;
            return true;
        }

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        date = null;
        return false;
    }
}

public record CreateWarrantyItemRequest(
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);

public record UpdateWarrantyItemRequest(
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn);

public record WarrantyItemResponse(
    Guid ItemId,
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);
