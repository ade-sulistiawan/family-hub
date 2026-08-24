using System.Net;
using System.Net.Http.Json;

namespace FamilyHub.Api.IntegrationTests;

public class ChoreCreationTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public ChoreCreationTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authenticated_family_member_can_create_a_one_off_chore_for_their_household()
    {
        var client = SignedInClient("chore-creator");
        var household = await Onboard(client, "Alex");
        var scheduledDate = new DateOnly(2026, 8, 25);

        var response = await client.PostAsJsonAsync("/api/chores", new
        {
            title = "Take bins out",
            assignedFamilyMemberId = household.FamilyMemberId,
            scheduledDate,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreatedChoreResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.ChoreId);
        Assert.NotEqual(Guid.Empty, created.ChoreOccurrenceId);
        Assert.Equal("Take bins out", created.Title);
        Assert.Equal(household.FamilyMemberId, created.AssignedFamilyMemberId);
        Assert.Equal(scheduledDate, created.ScheduledDate);
        Assert.Equal("OneOff", created.Recurrence);
    }

    [Fact]
    public async Task Family_member_cannot_assign_a_chore_outside_their_household()
    {
        var client = SignedInClient("isolated-chore-creator");
        await Onboard(client, "Alex");
        var otherHousehold = await Onboard(SignedInClient("other-household-member"), "Sam");

        var response = await client.PostAsJsonAsync("/api/chores", new
        {
            title = "Take bins out",
            assignedFamilyMemberId = otherHousehold.FamilyMemberId,
            scheduledDate = new DateOnly(2026, 8, 25),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Signed_in_account_must_complete_household_onboarding_before_creating_a_chore()
    {
        var client = SignedInClient("not-yet-onboarded-chore-creator");

        var response = await client.PostAsJsonAsync("/api/chores", new
        {
            title = "Take bins out",
            assignedFamilyMemberId = Guid.NewGuid(),
            scheduledDate = new DateOnly(2026, 8, 25),
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Family_member_can_list_only_their_households_chore_occurrences()
    {
        var client = SignedInClient("chore-list-owner");
        var household = await Onboard(client, "Alex");
        await CreateChore(client, "Take bins out", household.FamilyMemberId, new DateOnly(2026, 8, 25));

        var otherClient = SignedInClient("other-chore-list-owner");
        var otherHousehold = await Onboard(otherClient, "Sam");
        await CreateChore(otherClient, "Water plants", otherHousehold.FamilyMemberId, new DateOnly(2026, 8, 26));

        var chores = await client.GetFromJsonAsync<List<ListedChoreResponse>>("/api/chores");

        var chore = Assert.Single(chores!);
        Assert.Equal("Take bins out", chore.Title);
        Assert.Equal("Alex", chore.AssignedDisplayName);
        Assert.Equal(new DateOnly(2026, 8, 25), chore.ScheduledDate);
        Assert.Null(chore.CompletedAt);
    }

    [Fact]
    public async Task Chore_title_must_be_between_one_and_120_characters()
    {
        var client = SignedInClient("chore-title-validator");
        var household = await Onboard(client, "Alex");

        var blankTitleResponse = await client.PostAsJsonAsync("/api/chores", new
        {
            title = "   ",
            assignedFamilyMemberId = household.FamilyMemberId,
            scheduledDate = new DateOnly(2026, 8, 25),
        });
        var longTitleResponse = await client.PostAsJsonAsync("/api/chores", new
        {
            title = new string('x', 121),
            assignedFamilyMemberId = household.FamilyMemberId,
            scheduledDate = new DateOnly(2026, 8, 25),
        });

        Assert.Equal(HttpStatusCode.BadRequest, blankTitleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longTitleResponse.StatusCode);
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }

    private static async Task<HouseholdSetupResponse> Onboard(HttpClient client, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/household", new { displayName });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HouseholdSetupResponse>())!;
    }

    private static async Task CreateChore(
        HttpClient client,
        string title,
        Guid assignedFamilyMemberId,
        DateOnly scheduledDate)
    {
        var response = await client.PostAsJsonAsync("/api/chores", new
        {
            title,
            assignedFamilyMemberId,
            scheduledDate,
        });
        response.EnsureSuccessStatusCode();
    }

    private sealed record HouseholdSetupResponse(Guid FamilyMemberId);

    private sealed record CreatedChoreResponse(
        Guid ChoreId,
        Guid ChoreOccurrenceId,
        string Title,
        Guid AssignedFamilyMemberId,
        DateOnly ScheduledDate,
        string Recurrence);

    private sealed record ListedChoreResponse(
        string Title,
        string AssignedDisplayName,
        DateOnly ScheduledDate,
        DateTimeOffset? CompletedAt);
}