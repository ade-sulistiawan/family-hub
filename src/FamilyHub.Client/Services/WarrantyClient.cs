using System.Net.Http.Json;
using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

namespace FamilyHub.Services;

public class WarrantyClient(HttpClient http)
{
    public async Task<IReadOnlyList<WarrantyItem>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<WarrantyItem>>("/api/warranty-items") ?? [];

    public async Task CreateWithDocumentAsync(
        string name,
        DateOnly? purchasedOn,
        DateOnly? warrantyExpiresOn,
        IBrowserFile document)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(name), "name");
        if (purchasedOn is not null)
        {
            content.Add(new StringContent(purchasedOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "purchasedOn");
        }
        if (warrantyExpiresOn is not null)
        {
            content.Add(new StringContent(warrantyExpiresOn.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "warrantyExpiresOn");
        }

        var documentContent = new StreamContent(document.OpenReadStream(10 * 1024 * 1024));
        documentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(document.ContentType);
        content.Add(documentContent, "document", document.Name);

        var response = await http.PostAsync("/api/warranty-items/with-document", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Guid itemId, string name, DateOnly? purchasedOn, DateOnly? warrantyExpiresOn)
    {
        var response = await http.PutAsJsonAsync($"/api/warranty-items/{itemId}", new
        {
            name,
            purchasedOn,
            warrantyExpiresOn,
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task ReplaceDocumentAsync(Guid itemId, IBrowserFile document)
    {
        using var content = new MultipartFormDataContent();
        var documentContent = new StreamContent(document.OpenReadStream(10 * 1024 * 1024));
        documentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(document.ContentType);
        content.Add(documentContent, "document", document.Name);

        var response = await http.PostAsync($"/api/warranty-items/{itemId}/document", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid itemId)
    {
        var response = await http.DeleteAsync($"/api/warranty-items/{itemId}");
        response.EnsureSuccessStatusCode();
    }
}

public record WarrantyItem(
    Guid ItemId,
    string Name,
    DateOnly? PurchasedOn,
    DateOnly? WarrantyExpiresOn,
    string? DocumentExternalId);
