using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Chores;

public static class ChoreEndpoints
{
    public static RouteGroupBuilder MapChoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chores").RequireAuthorization();
        group.MapPost("/", Create);
        return group;
    }

    private static async Task<IResult> Create(
        CreateChoreRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var assigneeBelongsToHousehold = await db.FamilyMembers.AnyAsync(member =>
            member.Id == request.AssignedFamilyMemberId &&
            member.HouseholdId == currentMember.HouseholdId);

        if (!assigneeBelongsToHousehold)
        {
            return Results.BadRequest("The assigned Family Member does not belong to this Household.");
        }

        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMember.HouseholdId,
            Title = request.Title,
            AssignedFamilyMemberId = request.AssignedFamilyMemberId,
            Recurrence = ChoreRecurrence.OneOff,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var occurrence = new ChoreOccurrence
        {
            Id = Guid.NewGuid(),
            ChoreId = chore.Id,
            ScheduledDate = request.ScheduledDate,
        };

        db.Chores.Add(chore);
        db.ChoreOccurrences.Add(occurrence);
        await db.SaveChangesAsync();

        return Results.Created($"/api/chores/{chore.Id}", new ChoreResponse(
            chore.Id,
            occurrence.Id,
            chore.Title,
            chore.AssignedFamilyMemberId,
            occurrence.ScheduledDate,
            chore.Recurrence.ToString()));
    }
}

public record CreateChoreRequest(string Title, Guid AssignedFamilyMemberId, DateOnly ScheduledDate);

public record ChoreResponse(
    Guid ChoreId,
    Guid ChoreOccurrenceId,
    string Title,
    Guid AssignedFamilyMemberId,
    DateOnly ScheduledDate,
    string Recurrence);