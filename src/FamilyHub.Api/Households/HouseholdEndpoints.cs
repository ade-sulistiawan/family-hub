using System.Security.Claims;
using FamilyHub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Households;

public static class HouseholdEndpoints
{
    public static RouteGroupBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/household").RequireAuthorization();

        group.MapGet("/mine", GetMine);
        group.MapPost("/", Create);
        group.MapPost("/join", Join);

        return group;
    }

    private static async Task<IResult> GetMine(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var member = await FindCurrentMember(user, db);
        if (member is null)
        {
            return Results.NotFound();
        }

        var household = await db.Households.SingleAsync(h => h.Id == member.HouseholdId);
        return Results.Ok(ToResponse(household, member));
    }

    private static async Task<IResult> Create(CreateHouseholdRequest request, ClaimsPrincipal user, FamilyHubDbContext db)
    {
        if (await FindCurrentMember(user, db) is not null)
        {
            return Results.Conflict("This Google account already belongs to a Household.");
        }

        var household = new Household
        {
            Id = Guid.NewGuid(),
            JoinCode = JoinCodeGenerator.Generate(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Households.Add(household);

        var member = await EnrollMember(household, request.DisplayName, user, db);

        return Results.Created("/api/household/mine", ToResponse(household, member));
    }

    private static async Task<IResult> Join(JoinHouseholdRequest request, ClaimsPrincipal user, FamilyHubDbContext db)
    {
        if (await FindCurrentMember(user, db) is not null)
        {
            return Results.Conflict("This Google account already belongs to a Household.");
        }

        var normalizedJoinCode = request.JoinCode.Trim().ToUpperInvariant();
        var household = await db.Households.SingleOrDefaultAsync(h => h.JoinCode == normalizedJoinCode);
        if (household is null)
        {
            return Results.NotFound();
        }

        var member = await EnrollMember(household, request.DisplayName, user, db);

        return Results.Ok(ToResponse(household, member));
    }

    private static async Task<FamilyMember> EnrollMember(Household household, string displayName, ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var member = new FamilyMember
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            GoogleSubjectId = GetSubjectId(user),
            DisplayName = displayName,
            Email = GetEmail(user),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();

        return member;
    }

    private static Task<FamilyMember?> FindCurrentMember(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var subjectId = GetSubjectId(user);
        return db.FamilyMembers.SingleOrDefaultAsync(m => m.GoogleSubjectId == subjectId);
    }

    private static string GetSubjectId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no subject claim.");

    private static string GetEmail(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    private static HouseholdResponse ToResponse(Household household, FamilyMember member) => new(
        HouseholdId: household.Id,
        JoinCode: household.JoinCode,
        FamilyMemberId: member.Id,
        DisplayName: member.DisplayName);
}

public record CreateHouseholdRequest(string DisplayName);

public record JoinHouseholdRequest(string JoinCode, string DisplayName);

public record HouseholdResponse(Guid HouseholdId, string JoinCode, Guid FamilyMemberId, string DisplayName);
