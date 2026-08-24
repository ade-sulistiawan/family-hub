using System.Security.Claims;
using FamilyHub.Api.Authentication;
using FamilyHub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyHub.Api.Medications;

public static class MedicationEndpoints
{
    public static RouteGroupBuilder MapMedicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/medications").RequireAuthorization();
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapGet("/dose-logs", GetDoseLogs);
        group.MapPost("/{medicationId:guid}/dose-logs", LogDose);
        return group;
    }

    private static async Task<IResult> GetAll(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var medications = await (
            from medication in db.Medications.AsNoTracking()
            join assignee in db.FamilyMembers on medication.AssignedFamilyMemberId equals assignee.Id
            where medication.HouseholdId == currentMember.HouseholdId
            orderby medication.Name
            select new MedicationResponse(
                medication.Id,
                medication.Name,
                medication.Dosage,
                medication.AssignedFamilyMemberId,
                assignee.DisplayName,
                medication.Kind.ToString(),
                medication.ScheduledTime,
                medication.MinimumHoursBetweenDoses))
            .ToListAsync();

        return Results.Ok(medications);
    }

    private static async Task<IResult> Create(
        CreateMedicationRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        var name = request.Name?.Trim();
        var dosage = request.Dosage?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120 ||
            string.IsNullOrEmpty(dosage) || dosage.Length > 120)
        {
            return Results.BadRequest("Medication name and dosage must be between 1 and 120 characters.");
        }

        if (!Enum.TryParse<MedicationKind>(request.Kind, true, out var kind))
        {
            return Results.BadRequest("Medication kind must be Scheduled or Prn.");
        }

        if (kind == MedicationKind.Scheduled && request.ScheduledTime is null)
        {
            return Results.BadRequest("Scheduled Medication requires a schedule time.");
        }

        if (kind == MedicationKind.Prn && request.MinimumHoursBetweenDoses is not > 0)
        {
            return Results.BadRequest("PRN Medication requires a positive minimum dose interval.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var assignee = await db.FamilyMembers.SingleOrDefaultAsync(member =>
            member.Id == request.AssignedFamilyMemberId &&
            member.HouseholdId == currentMember.HouseholdId);
        if (assignee is null)
        {
            return Results.BadRequest("The assigned Family Member does not belong to this Household.");
        }

        var medication = new Medication
        {
            Id = Guid.NewGuid(),
            HouseholdId = currentMember.HouseholdId,
            AssignedFamilyMemberId = assignee.Id,
            Name = name,
            Dosage = dosage,
            Kind = kind,
            ScheduledTime = kind == MedicationKind.Scheduled ? request.ScheduledTime : null,
            MinimumHoursBetweenDoses = kind == MedicationKind.Prn ? request.MinimumHoursBetweenDoses : null,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Medications.Add(medication);
        await db.SaveChangesAsync();

        return Results.Created($"/api/medications/{medication.Id}", new MedicationResponse(
            medication.Id,
            medication.Name,
            medication.Dosage,
            medication.AssignedFamilyMemberId,
            assignee.DisplayName,
            medication.Kind.ToString(),
            medication.ScheduledTime,
            medication.MinimumHoursBetweenDoses));
    }

    private static async Task<IResult> GetDoseLogs(ClaimsPrincipal user, FamilyHubDbContext db)
    {
        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var logs = await (
            from log in db.DoseLogs.AsNoTracking()
            join medication in db.Medications on log.MedicationId equals medication.Id
            where medication.HouseholdId == currentMember.HouseholdId
            orderby log.LoggedAt descending
            select new DoseLogResponse(
                log.Id,
                log.MedicationId,
                log.FamilyMemberId,
                log.Status.ToString(),
                log.LoggedAt))
            .ToListAsync();

        return Results.Ok(logs);
    }

    private static async Task<IResult> LogDose(
        Guid medicationId,
        CreateDoseLogRequest request,
        ClaimsPrincipal user,
        FamilyHubDbContext db)
    {
        if (!Enum.TryParse<DoseLogStatus>(request.Status, true, out var status))
        {
            return Results.BadRequest("Dose Log status must be Taken, Skipped, or Missed.");
        }

        var currentMember = await CurrentFamilyMember.FindAsync(user, db);
        if (currentMember is null)
        {
            return Results.NotFound();
        }

        var medication = await db.Medications.SingleOrDefaultAsync(candidate =>
            candidate.Id == medicationId &&
            candidate.HouseholdId == currentMember.HouseholdId);
        if (medication is null)
        {
            return Results.NotFound();
        }

        var log = new DoseLog
        {
            Id = Guid.NewGuid(),
            MedicationId = medication.Id,
            FamilyMemberId = medication.AssignedFamilyMemberId,
            Status = status,
            LoggedAt = DateTimeOffset.UtcNow,
        };

        db.DoseLogs.Add(log);
        await db.SaveChangesAsync();

        return Results.Created($"/api/medications/dose-logs/{log.Id}", new DoseLogResponse(
            log.Id,
            log.MedicationId,
            log.FamilyMemberId,
            log.Status.ToString(),
            log.LoggedAt));
    }
}

public record CreateMedicationRequest(
    string Name,
    string Dosage,
    Guid AssignedFamilyMemberId,
    string Kind,
    TimeOnly? ScheduledTime,
    int? MinimumHoursBetweenDoses);

public record MedicationResponse(
    Guid MedicationId,
    string Name,
    string Dosage,
    Guid AssignedFamilyMemberId,
    string AssignedDisplayName,
    string Kind,
    TimeOnly? ScheduledTime,
    int? MinimumHoursBetweenDoses);

public record CreateDoseLogRequest(string Status);

public record DoseLogResponse(
    Guid DoseLogId,
    Guid MedicationId,
    Guid FamilyMemberId,
    string Status,
    DateTimeOffset LoggedAt);
