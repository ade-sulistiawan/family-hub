using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using FamilyHub.Api.Items;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Warranties;

public static class WarrantyEndpoints
{
    public static RouteGroupBuilder MapWarrantyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/warranty-items").RequireAuthorization();
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
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
}

public record CreateWarrantyItemRequest(
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);

public record WarrantyItemResponse(
    Guid ItemId,
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);
