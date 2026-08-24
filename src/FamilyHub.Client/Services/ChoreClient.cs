using System.Net.Http.Json;

namespace FamilyHub.Services;

public class ChoreClient(HttpClient http)
{
    public async Task<IReadOnlyList<ChoreListItem>> GetAllAsync()
    {
        return await http.GetFromJsonAsync<List<ChoreListItem>>("/api/chores") ?? [];
    }

    public async Task CreateAsync(string title, Guid assignedFamilyMemberId, DateOnly scheduledDate)
    {
        var response = await http.PostAsJsonAsync("/api/chores", new
        {
            title,
            assignedFamilyMemberId,
            scheduledDate,
        });

        response.EnsureSuccessStatusCode();
    }
}

public record ChoreListItem(
    Guid ChoreId,
    Guid ChoreOccurrenceId,
    string Title,
    Guid AssignedFamilyMemberId,
    string AssignedDisplayName,
    DateOnly ScheduledDate,
    DateTimeOffset? CompletedAt);