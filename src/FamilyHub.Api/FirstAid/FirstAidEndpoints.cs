using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using FamilyHub.Api.Expiry;
using FamilyHub.Api.Items;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.FirstAid;

public static class FirstAidEndpoints
{
    public static RouteGroupBuilder MapFirstAidEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/first-aid-items").RequireAuthorization();
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
            join stock in db.StockFacets on item.Id equals stock.ItemId
            join expiry in db.ExpiryFacets on item.Id equals expiry.ItemId
            where item.HouseholdId == currentMember.HouseholdId
            orderby stock.Quantity <= stock.LowStockThreshold descending, item.Name
            select new FirstAidItemResponse(
                item.Id,
                item.Name,
                stock.Quantity,
                stock.LowStockThreshold,
                expiry.ExpiresOn,
                expiry.LeadTimeDays,
                stock.Type.ToString()))
            .ToListAsync();

        return Results.Ok(items);
    }

    private static async Task<IResult> Create(
        CreateFirstAidItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.Quantity < 0 || request.LowStockThreshold < 0)
        {
            return Results.BadRequest("Stock values cannot be negative.");
        }

        if (request.LeadTimeDays is < 0 or > 3650)
        {
            return Results.BadRequest("Lead Time must be between 0 and 3650 days.");
        }

        if (!Enum.TryParse<FirstAidItemType>(request.Type, true, out var type))
        {
            return Results.BadRequest("Type must be one of: Other, Tablet, Syrup, Ointment, Spray, Bandage, Equipment.");
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
        var stock = new StockFacet
        {
            ItemId = item.Id,
            Quantity = request.Quantity,
            LowStockThreshold = request.LowStockThreshold,
            Type = type,
        };
        var expiry = new ExpiryFacet
        {
            ItemId = item.Id,
            ExpiresOn = request.ExpiresOn,
            LeadTimeDays = request.LeadTimeDays,
        };

        db.Items.Add(item);
        db.StockFacets.Add(stock);
        db.ExpiryFacets.Add(expiry);
        await db.SaveChangesAsync();

        return Results.Created($"/api/first-aid-items/{item.Id}", new FirstAidItemResponse(
            item.Id,
            item.Name,
            stock.Quantity,
            stock.LowStockThreshold,
            expiry.ExpiresOn,
            expiry.LeadTimeDays,
            stock.Type.ToString()));
    }

    private static async Task<IResult> Update(
        Guid itemId,
        UpdateFirstAidItemRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120)
        {
            return Results.BadRequest("Item name must be between 1 and 120 characters.");
        }

        if (request.Quantity < 0 || request.LowStockThreshold < 0)
        {
            return Results.BadRequest("Stock values cannot be negative.");
        }

        if (request.LeadTimeDays is < 0 or > 3650)
        {
            return Results.BadRequest("Lead Time must be between 0 and 3650 days.");
        }

        if (!Enum.TryParse<FirstAidItemType>(request.Type, true, out var type))
        {
            return Results.BadRequest("Type must be one of: Other, Tablet, Syrup, Ointment, Spray, Bandage, Equipment.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var item = await db.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.HouseholdId == currentMember.HouseholdId,
            cancellationToken);
        var stock = await db.StockFacets.SingleOrDefaultAsync(candidate => candidate.ItemId == itemId, cancellationToken);
        var expiry = await db.ExpiryFacets.SingleOrDefaultAsync(candidate => candidate.ItemId == itemId, cancellationToken);
        if (item is null || stock is null || expiry is null)
        {
            return Results.NotFound();
        }

        item.Name = name;
        stock.Quantity = request.Quantity;
        stock.LowStockThreshold = request.LowStockThreshold;
        stock.Type = type;
        expiry.ExpiresOn = request.ExpiresOn;
        expiry.LeadTimeDays = request.LeadTimeDays;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FirstAidItemResponse(
            item.Id,
            item.Name,
            stock.Quantity,
            stock.LowStockThreshold,
            expiry.ExpiresOn,
            expiry.LeadTimeDays,
            stock.Type.ToString()));
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

public record CreateFirstAidItemRequest(
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays,
    string Type);

public record UpdateFirstAidItemRequest(
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays,
    string Type);

public record FirstAidItemResponse(
    Guid ItemId,
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays,
    string Type);
