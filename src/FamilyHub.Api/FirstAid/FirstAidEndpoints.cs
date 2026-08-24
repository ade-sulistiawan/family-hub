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
                expiry.LeadTimeDays))
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
            expiry.LeadTimeDays));
    }
}

public record CreateFirstAidItemRequest(
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays);

public record FirstAidItemResponse(
    Guid ItemId,
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays);
