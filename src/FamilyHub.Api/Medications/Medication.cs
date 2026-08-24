namespace FamilyHub.Api.Medications;

public class Medication
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required Guid AssignedFamilyMemberId { get; init; }
    public required string Name { get; init; }
    public required string Dosage { get; init; }
    public MedicationKind Kind { get; init; }
    public TimeOnly? ScheduledTime { get; init; }
    public int? MinimumHoursBetweenDoses { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum MedicationKind
{
    Scheduled,
    Prn,
}
