namespace FamilyHub.Api.Medications;

public class DoseLog
{
    public Guid Id { get; init; }
    public required Guid MedicationId { get; init; }
    public required Guid FamilyMemberId { get; init; }
    public DoseLogStatus Status { get; init; }
    public DateTimeOffset LoggedAt { get; init; }
}

public enum DoseLogStatus
{
    Taken,
    Skipped,
    Missed,
}
