using System.Net;
using System.Net.Http.Json;

namespace FamilyHub.Api.IntegrationTests;

public class TrackerFeatureTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public TrackerFeatureTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Family_member_can_add_and_list_an_expiring_item_for_their_household()
    {
        var client = SignedInClient("expiry-owner");
        await Onboard(client, "Alex");

        var createResponse = await client.PostAsJsonAsync("/api/expiry-items", new
        {
            name = "Milk",
            expiresOn = new DateOnly(2026, 8, 30),
            leadTimeDays = 3,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var items = await client.GetFromJsonAsync<List<ExpiryItemResponse>>("/api/expiry-items");
        var item = Assert.Single(items!);
        Assert.Equal("Milk", item.Name);
        Assert.Equal(new DateOnly(2026, 8, 30), item.ExpiresOn);
        Assert.Equal(3, item.LeadTimeDays);
    }

    [Fact]
    public async Task Family_member_can_add_and_list_a_warranty_item_with_a_document_reference()
    {
        var client = SignedInClient("warranty-owner");
        await Onboard(client, "Alex");

        var createResponse = await client.PostAsJsonAsync("/api/warranty-items", new
        {
            name = "Washing machine",
            purchasedOn = new DateOnly(2026, 8, 1),
            warrantyExpiresOn = new DateOnly(2028, 8, 1),
            documentExternalId = "paperless-4821",
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var items = await client.GetFromJsonAsync<List<WarrantyItemResponse>>("/api/warranty-items");
        var item = Assert.Single(items!);
        Assert.Equal("Washing machine", item.Name);
        Assert.Equal(new DateOnly(2028, 8, 1), item.WarrantyExpiresOn);
        Assert.Equal("paperless-4821", item.DocumentExternalId);
    }

    [Fact]
    public async Task First_aid_item_shares_stock_and_expiry_facets()
    {
        var client = SignedInClient("first-aid-owner");
        await Onboard(client, "Alex");

        var createResponse = await client.PostAsJsonAsync("/api/first-aid-items", new
        {
            name = "Adhesive bandages",
            quantity = 12,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 6, 30),
            leadTimeDays = 30,
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var stockItems = await client.GetFromJsonAsync<List<FirstAidItemResponse>>("/api/first-aid-items");
        var stockItem = Assert.Single(stockItems!);
        Assert.Equal(12, stockItem.Quantity);
        Assert.Equal(5, stockItem.LowStockThreshold);

        var expiryItems = await client.GetFromJsonAsync<List<ExpiryItemResponse>>("/api/expiry-items");
        var expiryItem = Assert.Single(expiryItems!);
        Assert.Equal(stockItem.ItemId, expiryItem.ItemId);
        Assert.Equal(new DateOnly(2028, 6, 30), expiryItem.ExpiresOn);
    }

    [Fact]
    public async Task Family_member_can_manage_scheduled_and_prn_medication_with_dose_logs()
    {
        var client = SignedInClient("medication-owner");
        var familyMemberId = await Onboard(client, "Alex");

        var scheduledResponse = await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Vitamin D",
            dosage = "1 tablet",
            assignedFamilyMemberId = familyMemberId,
            kind = "Scheduled",
            scheduledTime = new TimeOnly(8, 0),
            minimumHoursBetweenDoses = (int?)null,
        });
        var prnResponse = await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Ibuprofen",
            dosage = "200 mg",
            assignedFamilyMemberId = familyMemberId,
            kind = "Prn",
            scheduledTime = (TimeOnly?)null,
            minimumHoursBetweenDoses = 6,
        });
        var unsafePrnResponse = await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Paracetamol",
            dosage = "500 mg",
            assignedFamilyMemberId = familyMemberId,
            kind = "Prn",
            scheduledTime = (TimeOnly?)null,
            minimumHoursBetweenDoses = (int?)null,
        });

        Assert.Equal(HttpStatusCode.Created, scheduledResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, prnResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsafePrnResponse.StatusCode);
        var prn = (await prnResponse.Content.ReadFromJsonAsync<MedicationResponse>())!;

        var logResponse = await client.PostAsJsonAsync(
            $"/api/medications/{prn.MedicationId}/dose-logs",
            new { status = "Taken" });

        Assert.Equal(HttpStatusCode.Created, logResponse.StatusCode);
        var medications = await client.GetFromJsonAsync<List<MedicationResponse>>("/api/medications");
        Assert.Contains(medications!, medication => medication.Kind == "Scheduled" && medication.ScheduledTime == new TimeOnly(8, 0));
        Assert.Contains(medications!, medication => medication.Kind == "Prn" && medication.MinimumHoursBetweenDoses == 6);
        var logs = await client.GetFromJsonAsync<List<DoseLogResponse>>("/api/medications/dose-logs");
        Assert.Equal(prn.MedicationId, Assert.Single(logs!).MedicationId);
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }

    private static async Task<Guid> Onboard(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/household", new { displayName });
        response.EnsureSuccessStatusCode();
        var household = await response.Content.ReadFromJsonAsync<HouseholdSetupResponse>();
        return household!.FamilyMemberId;
    }

    private sealed record ExpiryItemResponse(
        Guid ItemId,
        string Name,
        DateOnly ExpiresOn,
        int LeadTimeDays);

    private sealed record WarrantyItemResponse(
        Guid ItemId,
        string Name,
        DateOnly? PurchasedOn,
        DateOnly? WarrantyExpiresOn,
        string? DocumentExternalId);

    private sealed record FirstAidItemResponse(
        Guid ItemId,
        string Name,
        int Quantity,
        int LowStockThreshold,
        DateOnly ExpiresOn,
        int LeadTimeDays);

    private sealed record HouseholdSetupResponse(Guid FamilyMemberId);

    private sealed record MedicationResponse(
        Guid MedicationId,
        string Name,
        string Dosage,
        Guid AssignedFamilyMemberId,
        string AssignedDisplayName,
        string Kind,
        TimeOnly? ScheduledTime,
        int? MinimumHoursBetweenDoses);

    private sealed record DoseLogResponse(
        Guid DoseLogId,
        Guid MedicationId,
        Guid FamilyMemberId,
        string Status,
        DateTimeOffset LoggedAt);
}
