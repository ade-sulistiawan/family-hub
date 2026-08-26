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

    public async Task CompleteAsync(Guid choreOccurrenceId)
    {
        var response = await http.PutAsync(
            $"/api/chores/occurrences/{choreOccurrenceId}/completion",
            null);

        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Guid choreId, string title, Guid assignedFamilyMemberId, DateOnly scheduledDate)
    {
        var response = await http.PutAsJsonAsync($"/api/chores/{choreId}", new
        {
            title,
            assignedFamilyMemberId,
            scheduledDate,
        });

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid choreId)
    {
        var response = await http.DeleteAsync($"/api/chores/{choreId}");
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