namespace FamilyHub.Api.Medications;

public class Medication
{
    public Guid Id { get; init; }
    public required Guid HouseholdId { get; init; }
    public required Guid AssignedFamilyMemberId { get; set; }
    public required string Name { get; set; }
    public required string Dosage { get; set; }
    public MedicationKind Kind { get; set; }
    public TimeOnly? ScheduledTime { get; set; }
    public int? MinimumHoursBetweenDoses { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum MedicationKind
{
    Scheduled,
    Prn,
}
