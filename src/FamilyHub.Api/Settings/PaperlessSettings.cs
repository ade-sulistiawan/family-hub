namespace FamilyHub.Api.Settings;

public class PaperlessSettings
{
    public required Guid HouseholdId { get; init; }
    public required string BaseUrl { get; set; }
    public required string EncryptedApiToken { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}