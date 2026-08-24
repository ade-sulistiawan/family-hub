using System.Security.Claims;
using FamilyHub.Api.Data;
using FamilyHub.Api.Households;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Authentication;

public static class CurrentFamilyMember
{
    public static Task<FamilyMember?> FindAsync(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var subjectId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no subject claim.");
        return db.FamilyMembers.SingleOrDefaultAsync(member => member.GoogleSubjectId == subjectId);
    }
}