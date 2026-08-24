using System.Net.Http.Json;

namespace FamilyHub.Services;

public class ExpiryClient(HttpClient http)
{
    public async Task<IReadOnlyList<ExpiryItem>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<ExpiryItem>>("/api/expiry-items") ?? [];

    public async Task CreateAsync(string name, DateOnly expiresOn, int leadTimeDays)
    {
        var response = await http.PostAsJsonAsync("/api/expiry-items", new { name, expiresOn, leadTimeDays });
        response.EnsureSuccessStatusCode();
    }
}

public record ExpiryItem(Guid ItemId, string Name, DateOnly ExpiresOn, int LeadTimeDays);
