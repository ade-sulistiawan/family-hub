using System.Net.Http.Json;

namespace FamilyHub.Services;

public class FirstAidClient(HttpClient http)
{
    public async Task<IReadOnlyList<FirstAidItem>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<FirstAidItem>>("/api/first-aid-items") ?? [];

    public async Task CreateAsync(
        string name,
        int quantity,
        int lowStockThreshold,
        DateOnly expiresOn,
        int leadTimeDays)
    {
        var response = await http.PostAsJsonAsync("/api/first-aid-items", new
        {
            name,
            quantity,
            lowStockThreshold,
            expiresOn,
            leadTimeDays,
        });
        response.EnsureSuccessStatusCode();
    }
}

public record FirstAidItem(
    Guid ItemId,
    string Name,
    int Quantity,
    int LowStockThreshold,
    DateOnly ExpiresOn,
    int LeadTimeDays);
