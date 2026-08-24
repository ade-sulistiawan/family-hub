using System.Net.Http.Json;

namespace FamilyHub.Services;

public class PaperlessSettingsClient(HttpClient http)
{
    public async Task<PaperlessSettingsInfo> GetAsync() =>
        await http.GetFromJsonAsync<PaperlessSettingsInfo>("/api/settings/paperless")
        ?? new PaperlessSettingsInfo(null, false);

    public async Task SaveAsync(string baseUrl, string apiToken)
    {
        var response = await http.PutAsJsonAsync("/api/settings/paperless", new { baseUrl, apiToken });
        response.EnsureSuccessStatusCode();
    }
}

public record PaperlessSettingsInfo(string? BaseUrl, bool Configured);