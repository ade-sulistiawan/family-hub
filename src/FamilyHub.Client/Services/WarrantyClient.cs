using System.Net.Http.Json;

namespace FamilyHub.Services;

public class WarrantyClient(HttpClient http)
{
    public async Task<IReadOnlyList<WarrantyItem>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<WarrantyItem>>("/api/warranty-items") ?? [];

    public async Task CreateAsync(
        string name,
        DateOnly? purchasedOn,
        DateOnly? warrantyExpiresOn,
        string? documentExternalId)
    {
        var response = await http.PostAsJsonAsync("/api/warranty-items", new
        {
            name,
            purchasedOn,
            warrantyExpiresOn,
            documentExternalId,
        });
        response.EnsureSuccessStatusCode();
    }
}

public record WarrantyItem(
    Guid ItemId,
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);
