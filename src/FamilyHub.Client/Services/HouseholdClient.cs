using System.Net.Http.Json;

namespace FamilyHub.Services;

public class HouseholdClient(HttpClient http)
{
    public async Task<HouseholdInfo?> GetMineAsync()
    {
        var response = await http.GetAsync("/api/household/mine");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HouseholdInfo>();
    }

    public async Task<HouseholdInfo?> CreateAsync(string displayName)
    {
        var response = await http.PostAsJsonAsync("/api/household", new { displayName });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdInfo>()
            : null;
    }

    public async Task<HouseholdInfo?> JoinAsync(string joinCode, string displayName)
    {
        var response = await http.PostAsJsonAsync("/api/household/join", new { joinCode, displayName });
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<HouseholdInfo>()
            : null;
    }
}

public record HouseholdInfo(Guid HouseholdId, string JoinCode, Guid FamilyMemberId, string DisplayName);
