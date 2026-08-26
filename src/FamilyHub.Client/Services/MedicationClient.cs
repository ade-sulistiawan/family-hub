using System.Net.Http.Json;

namespace FamilyHub.Services;

public class MedicationClient(HttpClient http)
{
    public async Task<IReadOnlyList<MedicationItem>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<MedicationItem>>("/api/medications") ?? [];

    public async Task<IReadOnlyList<DoseLogItem>> GetDoseLogsAsync() =>
        await http.GetFromJsonAsync<List<DoseLogItem>>("/api/medications/dose-logs") ?? [];

    public async Task CreateAsync(
        string name,
        string dosage,
        Guid assignedFamilyMemberId,
        string kind,
        TimeOnly? scheduledTime,
        int? minimumHoursBetweenDoses)
    {
        var response = await http.PostAsJsonAsync("/api/medications", new
        {
            name,
            dosage,
            assignedFamilyMemberId,
            kind,
            scheduledTime,
            minimumHoursBetweenDoses,
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task LogDoseAsync(Guid medicationId, string status)
    {
        var response = await http.PostAsJsonAsync(
            $"/api/medications/{medicationId}/dose-logs",
            new { status });
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(
        Guid medicationId,
        string name,
        string dosage,
        Guid assignedFamilyMemberId,
        string kind,
        TimeOnly? scheduledTime,
        int? minimumHoursBetweenDoses)
    {
        var response = await http.PutAsJsonAsync($"/api/medications/{medicationId}", new
        {
            name,
            dosage,
            assignedFamilyMemberId,
            kind,
            scheduledTime,
            minimumHoursBetweenDoses,
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid medicationId)
    {
        var response = await http.DeleteAsync($"/api/medications/{medicationId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateDoseLogAsync(Guid doseLogId, string status)
    {
        var response = await http.PutAsJsonAsync($"/api/medications/dose-logs/{doseLogId}", new { status });
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> TryDeleteDoseLogAsync(Guid doseLogId)
    {
        var response = await http.DeleteAsync($"/api/medications/dose-logs/{doseLogId}");
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return false;
        }
        response.EnsureSuccessStatusCode();
        return true;
    }
}

public record MedicationItem(
    Guid MedicationId,
    string Name,
    string Dosage,
    Guid AssignedFamilyMemberId,
    string AssignedDisplayName,
    string Kind,
    TimeOnly? ScheduledTime,
    int? MinimumHoursBetweenDoses);

public record DoseLogItem(
    Guid DoseLogId,
    Guid MedicationId,
    Guid FamilyMemberId,
    string Status,
    DateTimeOffset LoggedAt);
