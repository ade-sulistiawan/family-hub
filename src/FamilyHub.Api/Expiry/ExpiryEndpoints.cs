using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using FamilyHub.Api.Items;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Expiry;

public static class ExpiryEndpoints
{
    public static RouteGroupBuilder MapExpiryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/expiry-items").RequireAuthorization();
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/{itemId:guid}", Update);
        group.MapDelete("/{itemId:guid}", Delete);
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
            join expiry in db.ExpiryFacets on item.Id equals expiry.ItemId
            where item.HouseholdId == currentMember.HouseholdId
            orderby expiry.ExpiresOn, item.Name
            select new ExpiryItemResponse(item.Id, item.Name, expiry.ExpiresOn, expiry.LeadTimeDays))
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> Create(
        CreateExpiryItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.LeadTimeDays is < 0 or > 3650)
        {
            return Results.BadRequest("Lead Time must be between 0 and 3650 days.");
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
        var expiry = new ExpiryFacet
        {
            ItemId = item.Id,
            ExpiresOn = request.ExpiresOn,
            LeadTimeDays = request.LeadTimeDays,
        };

        db.Items.Add(item);
        db.ExpiryFacets.Add(expiry);
        await db.SaveChangesAsync();

        return Results.Created($"/api/expiry-items/{item.Id}", new ExpiryItemResponse(
            item.Id,
            item.Name,
            expiry.ExpiresOn,
            expiry.LeadTimeDays));
    }

    private static async Task<IResult> Update(
        Guid itemId,
        UpdateExpiryItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.LeadTimeDays is < 0 or > 3650)
        {
            return Results.BadRequest("Lead Time must be between 0 and 3650 days.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var item = await db.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.HouseholdId == currentMember.HouseholdId,
            cancellationToken);
        var expiry = await db.ExpiryFacets.SingleOrDefaultAsync(
            candidate => candidate.ItemId == itemId,
            cancellationToken);
        if (item is null || expiry is null)
        {
            return Results.NotFound();
        }

        item.Name = name;
        expiry.ExpiresOn = request.ExpiresOn;
        expiry.LeadTimeDays = request.LeadTimeDays;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ExpiryItemResponse(item.Id, item.Name, expiry.ExpiresOn, expiry.LeadTimeDays));
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
}

public record CreateExpiryItemRequest(string Name, DateOnly ExpiresOn, int LeadTimeDays);

public record UpdateExpiryItemRequest(string Name, DateOnly ExpiresOn, int LeadTimeDays);

public record ExpiryItemResponse(Guid ItemId, string Name, DateOnly ExpiresOn, int LeadTimeDays);
