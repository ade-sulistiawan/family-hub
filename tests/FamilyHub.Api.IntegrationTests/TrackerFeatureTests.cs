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
    public async Task Family_member_can_edit_and_delete_an_expiring_item()
    {
        var client = SignedInClient("expiry-editor");
        await Onboard(client, "Alex");
        var created = await (await client.PostAsJsonAsync("/api/expiry-items", new
        {
            name = "Milk",
            expiresOn = new DateOnly(2026, 8, 30),
            leadTimeDays = 3,
        })).Content.ReadFromJsonAsync<ExpiryItemResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/expiry-items/{created!.ItemId}", new
        {
            name = "Oat milk",
            expiresOn = new DateOnly(2026, 9, 15),
            leadTimeDays = 5,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await client.GetFromJsonAsync<List<ExpiryItemResponse>>("/api/expiry-items");
        var item = Assert.Single(updated!);
        Assert.Equal("Oat milk", item.Name);
        Assert.Equal(new DateOnly(2026, 9, 15), item.ExpiresOn);

        var deleteResponse = await client.DeleteAsync($"/api/expiry-items/{created.ItemId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ExpiryItemResponse>>("/api/expiry-items"))!);
    }

    [Fact]
    public async Task Family_member_cannot_edit_or_delete_an_expiring_item_outside_their_household()
    {
        var owner = SignedInClient("expiry-owner-for-guard");
        await Onboard(owner, "Alex");
        var created = await (await owner.PostAsJsonAsync("/api/expiry-items", new
        {
            name = "Milk",
            expiresOn = new DateOnly(2026, 8, 30),
            leadTimeDays = 3,
        })).Content.ReadFromJsonAsync<ExpiryItemResponse>();

        var outsider = SignedInClient("expiry-outsider");
        await Onboard(outsider, "Sam");

        var updateResponse = await outsider.PutAsJsonAsync($"/api/expiry-items/{created!.ItemId}", new
        {
            name = "Hijacked",
            expiresOn = new DateOnly(2026, 9, 15),
            leadTimeDays = 5,
        });
        var deleteResponse = await outsider.DeleteAsync($"/api/expiry-items/{created.ItemId}");

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
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
    public async Task Family_member_can_edit_a_warranty_items_metadata_without_touching_its_document_and_then_delete_it()
    {
        var client = SignedInClient("warranty-editor");
        await Onboard(client, "Alex");
        var created = await (await client.PostAsJsonAsync("/api/warranty-items", new
        {
            name = "Washing machine",
            purchasedOn = new DateOnly(2026, 8, 1),
            warrantyExpiresOn = new DateOnly(2028, 8, 1),
            documentExternalId = "paperless-4821",
        })).Content.ReadFromJsonAsync<WarrantyItemResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/warranty-items/{created!.ItemId}", new
        {
            name = "Washer-dryer",
            purchasedOn = new DateOnly(2026, 8, 2),
            warrantyExpiresOn = new DateOnly(2029, 8, 1),
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = Assert.Single((await client.GetFromJsonAsync<List<WarrantyItemResponse>>("/api/warranty-items"))!);
        Assert.Equal("Washer-dryer", updated.Name);
        Assert.Equal(new DateOnly(2029, 8, 1), updated.WarrantyExpiresOn);
        Assert.Equal("paperless-4821", updated.DocumentExternalId);

        var deleteResponse = await client.DeleteAsync($"/api/warranty-items/{created.ItemId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<WarrantyItemResponse>>("/api/warranty-items"))!);
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
    public async Task Family_member_can_edit_and_delete_a_first_aid_item()
    {
        var client = SignedInClient("first-aid-editor");
        await Onboard(client, "Alex");
        var created = await (await client.PostAsJsonAsync("/api/first-aid-items", new
        {
            name = "Adhesive bandages",
            quantity = 12,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 6, 30),
            leadTimeDays = 30,
        })).Content.ReadFromJsonAsync<FirstAidItemResponse>();

        var updateResponse = await client.PutAsJsonAsync($"/api/first-aid-items/{created!.ItemId}", new
        {
            name = "Adhesive bandages (large)",
            quantity = 3,
            lowStockThreshold = 5,
            expiresOn = new DateOnly(2028, 12, 31),
            leadTimeDays = 14,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = Assert.Single((await client.GetFromJsonAsync<List<FirstAidItemResponse>>("/api/first-aid-items"))!);
        Assert.Equal(3, updated.Quantity);
        Assert.Equal(new DateOnly(2028, 12, 31), updated.ExpiresOn);

        var deleteResponse = await client.DeleteAsync($"/api/first-aid-items/{created.ItemId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<FirstAidItemResponse>>("/api/first-aid-items"))!);
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

    [Fact]
    public async Task Family_member_can_edit_and_delete_a_medication()
    {
        var client = SignedInClient("medication-editor");
        var familyMemberId = await Onboard(client, "Alex");
        var created = (await (await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Vitamin D",
            dosage = "1 tablet",
            assignedFamilyMemberId = familyMemberId,
            kind = "Scheduled",
            scheduledTime = new TimeOnly(8, 0),
            minimumHoursBetweenDoses = (int?)null,
        })).Content.ReadFromJsonAsync<MedicationResponse>())!;

        var updateResponse = await client.PutAsJsonAsync($"/api/medications/{created.MedicationId}", new
        {
            name = "Vitamin D3",
            dosage = "2 tablets",
            assignedFamilyMemberId = familyMemberId,
            kind = "Scheduled",
            scheduledTime = new TimeOnly(9, 0),
            minimumHoursBetweenDoses = (int?)null,
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = Assert.Single((await client.GetFromJsonAsync<List<MedicationResponse>>("/api/medications"))!);
        Assert.Equal("Vitamin D3", updated.Name);
        Assert.Equal(new TimeOnly(9, 0), updated.ScheduledTime);

        var deleteResponse = await client.DeleteAsync($"/api/medications/{created.MedicationId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<MedicationResponse>>("/api/medications"))!);
    }

    [Fact]
    public async Task Family_member_can_correct_a_dose_log_but_can_only_hard_delete_an_exact_duplicate()
    {
        var client = SignedInClient("dose-log-editor");
        var familyMemberId = await Onboard(client, "Alex");
        var medication = (await (await client.PostAsJsonAsync("/api/medications", new
        {
            name = "Ibuprofen",
            dosage = "200 mg",
            assignedFamilyMemberId = familyMemberId,
            kind = "Prn",
            scheduledTime = (TimeOnly?)null,
            minimumHoursBetweenDoses = 6,
        })).Content.ReadFromJsonAsync<MedicationResponse>())!;
        var log = (await (await client.PostAsJsonAsync(
            $"/api/medications/{medication.MedicationId}/dose-logs",
            new { status = "Taken" })).Content.ReadFromJsonAsync<DoseLogResponse>())!;

        var correctResponse = await client.PutAsJsonAsync(
            $"/api/medications/dose-logs/{log.DoseLogId}",
            new { status = "Skipped" });

        Assert.Equal(HttpStatusCode.OK, correctResponse.StatusCode);
        var corrected = Assert.Single((await client.GetFromJsonAsync<List<DoseLogResponse>>("/api/medications/dose-logs"))!);
        Assert.Equal("Skipped", corrected.Status);

        var deleteWithoutDuplicateResponse = await client.DeleteAsync($"/api/medications/dose-logs/{log.DoseLogId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteWithoutDuplicateResponse.StatusCode);

        var duplicateLog = (await (await client.PostAsJsonAsync(
            $"/api/medications/{medication.MedicationId}/dose-logs",
            new { status = "Skipped" })).Content.ReadFromJsonAsync<DoseLogResponse>())!;

        var deleteDuplicateResponse = await client.DeleteAsync($"/api/medications/dose-logs/{duplicateLog.DoseLogId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteDuplicateResponse.StatusCode);
        var remaining = Assert.Single((await client.GetFromJsonAsync<List<DoseLogResponse>>("/api/medications/dose-logs"))!);
        Assert.Equal(log.DoseLogId, remaining.DoseLogId);
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
