using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FamilyHub.Api.IntegrationTests;

public class PaperlessIntegrationTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public PaperlessIntegrationTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Family_member_can_configure_paperless_for_their_household_without_exposing_the_token()
    {
        var client = SignedInClient("paperless-settings-owner");
        await Onboard(client, "Alex");

        var saveResponse = await client.PutAsJsonAsync("/api/settings/paperless", new
        {
            baseUrl = "https://paperless.example.test/",
            apiToken = "secret-paperless-token",
        });

        Assert.Equal(HttpStatusCode.NoContent, saveResponse.StatusCode);

        var settings = await client.GetFromJsonAsync<PaperlessSettingsResponse>("/api/settings/paperless");
        Assert.NotNull(settings);
        Assert.Equal("https://paperless.example.test/", settings.BaseUrl);
        Assert.True(settings.Configured);

        var responseBody = await (await client.GetAsync("/api/settings/paperless")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-paperless-token", responseBody);
        Assert.DoesNotContain("apiToken", responseBody);
    }

    [Fact]
    public async Task Family_member_can_upload_a_warranty_image_to_paperless_and_store_its_document_reference()
    {
        var client = SignedInClient("paperless-upload-owner");
        await Onboard(client, "Alex");
        await client.PutAsJsonAsync("/api/settings/paperless", new
        {
            baseUrl = "https://paperless.example.test/",
            apiToken = "secret-paperless-token",
        });
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Washing machine"), "name" },
            { new StringContent("2026-08-01"), "purchasedOn" },
            { new StringContent("2028-08-01"), "warrantyExpiresOn" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("receipt-image")), "document", "receipt.png" },
        };
        form.Last().Headers.ContentType = new("image/png");

        var createResponse = await client.PostAsync("/api/warranty-items/with-document", form);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<WarrantyItemResponse>();
        Assert.NotNull(created);
        Assert.Equal("4821", created.DocumentExternalId);

        var items = await client.GetFromJsonAsync<List<WarrantyItemResponse>>("/api/warranty-items");
        Assert.Equal("4821", Assert.Single(items!).DocumentExternalId);
    }

    [Fact]
    public async Task Family_member_can_fetch_the_thumbnail_of_an_uploaded_warranty_document()
    {
        var client = SignedInClient("paperless-thumbnail-owner");
        await Onboard(client, "Alex");
        await client.PutAsJsonAsync("/api/settings/paperless", new
        {
            baseUrl = "https://paperless.example.test/",
            apiToken = "secret-paperless-token",
        });
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Washing machine"), "name" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("receipt-image")), "document", "receipt.png" },
        };
        form.Last().Headers.ContentType = new("image/png");
        var createResponse = await client.PostAsync("/api/warranty-items/with-document", form);
        var created = await createResponse.Content.ReadFromJsonAsync<WarrantyItemResponse>();

        var thumbnailResponse = await client.GetAsync($"/api/warranty-items/{created!.ItemId}/document/thumbnail");

        Assert.Equal(HttpStatusCode.OK, thumbnailResponse.StatusCode);
        Assert.Equal("image/png", thumbnailResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, await thumbnailResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Family_member_can_fetch_the_full_size_preview_of_an_uploaded_warranty_document()
    {
        var client = SignedInClient("paperless-preview-owner");
        await Onboard(client, "Alex");
        await client.PutAsJsonAsync("/api/settings/paperless", new
        {
            baseUrl = "https://paperless.example.test/",
            apiToken = "secret-paperless-token",
        });
        using var form = new MultipartFormDataContent
        {
            { new StringContent("Washing machine"), "name" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("receipt-image")), "document", "receipt.png" },
        };
        form.Last().Headers.ContentType = new("image/png");
        var createResponse = await client.PostAsync("/api/warranty-items/with-document", form);
        var created = await createResponse.Content.ReadFromJsonAsync<WarrantyItemResponse>();

        var previewResponse = await client.GetAsync($"/api/warranty-items/{created!.ItemId}/document/preview");

        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.Equal("image/png", previewResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await previewResponse.Content.ReadAsByteArrayAsync());
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }

    private static async Task Onboard(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/household", new { displayName });
        response.EnsureSuccessStatusCode();
    }

    private sealed record PaperlessSettingsResponse(string? BaseUrl, bool Configured);

    private sealed record WarrantyItemResponse(
        Guid ItemId,
        string Name,
        DateOnly? PurchasedOn,
        DateOnly? WarrantyExpiresOn,
        string? DocumentExternalId);
}