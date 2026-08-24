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
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/occurrences/{choreOccurrenceId:guid}/completion", CompleteOccurrence);
        return group;
    }

    private static async Task<IResult> GetAll(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var chores = await (
            from occurrence in db.ChoreOccurrences.AsNoTracking()
            join chore in db.Chores on occurrence.ChoreId equals chore.Id
            join assignee in db.FamilyMembers on chore.AssignedFamilyMemberId equals assignee.Id
            where chore.HouseholdId == currentMember.HouseholdId
            orderby occurrence.ScheduledDate, chore.Title
            select new ChoreListItemResponse(
                chore.Id,
                occurrence.Id,
                chore.Title,
                chore.AssignedFamilyMemberId,
                assignee.DisplayName,
                occurrence.ScheduledDate,
                occurrence.CompletedAt))
            .ToListAsync();

        return Results.Ok(chores);
    }

    private static async Task<IResult> Create(
        CreateChoreRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 120)
        {
            return Results.BadRequest("Chore title must be between 1 and 120 characters.");
        }

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
            Title = title,
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

    private static async Task<IResult> CompleteOccurrence(
        Guid choreOccurrenceId,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var occurrence = await (
            from candidate in db.ChoreOccurrences
            join chore in db.Chores on candidate.ChoreId equals chore.Id
            where candidate.Id == choreOccurrenceId &&
                chore.HouseholdId == currentMember.HouseholdId
            select candidate)
            .SingleOrDefaultAsync();

        if (occurrence is null)
        {
            return Results.NotFound();
        }

        if (occurrence.CompletedAt is null)
        {
            occurrence.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.NoContent();
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

public record ChoreListItemResponse(
    Guid ChoreId,
    Guid ChoreOccurrenceId,
    string Title,
    Guid AssignedFamilyMemberId,
    string AssignedDisplayName,
    DateOnly ScheduledDate,
    DateTimeOffset? CompletedAt);