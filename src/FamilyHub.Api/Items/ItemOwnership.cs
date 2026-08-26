using FamilyHub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Items;

// Shared by every tracker built on Item + Facet (Expiry, Warranty, First-Aid): deleting the
// Item cascades its Facet row via the FK relationship configured in FamilyHubDbContext.
public static class ItemOwnership
{
    public static async Task<IResult> DeleteAsync(
        Guid itemId,
        Guid householdId,
        FamilyHubDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.SingleOrDefaultAsync(
            candidate => candidate.Id == itemId && candidate.HouseholdId == householdId,
            cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
