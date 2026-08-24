using System.Net;
using System.Net.Http.Json;
using FamilyHub.Api.Households;

namespace FamilyHub.Api.IntegrationTests;

public class HouseholdOnboardingTests : IClassFixture<FamilyHubApiFactory>
{
    private readonly FamilyHubApiFactory _factory;

    public HouseholdOnboardingTests(FamilyHubApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_returns_unauthorized_when_not_signed_in()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/household/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_returns_not_found_for_a_family_member_who_has_not_onboarded_yet()
    {
        var client = SignedInClient("brand-new-subject");

        var response = await client.GetAsync("/api/household/mine");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task First_sign_in_creates_a_household_the_family_member_can_then_fetch()
    {
        var client = SignedInClient("first-family-member");

        var createResponse = await client.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        Assert.NotNull(created);
        Assert.Equal("Alex", created!.DisplayName);
        Assert.NotEmpty(created.JoinCode);

        var mineResponse = await client.GetAsync("/api/household/mine");
        Assert.Equal(HttpStatusCode.OK, mineResponse.StatusCode);
        var mine = await mineResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        Assert.Equal(created.HouseholdId, mine!.HouseholdId);
    }

    [Fact]
    public async Task A_second_family_member_joins_the_same_household_via_the_join_code()
    {
        var firstMember = SignedInClient("household-creator");
        var created = await (await firstMember.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex")))
            .Content.ReadFromJsonAsync<HouseholdResponse>();

        var secondMember = SignedInClient("joining-family-member");
        var joinResponse = await secondMember.PostAsJsonAsync(
            "/api/household/join",
            new JoinHouseholdRequest(created!.JoinCode, "Sam"));

        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        var joined = await joinResponse.Content.ReadFromJsonAsync<HouseholdResponse>();
        Assert.Equal(created.HouseholdId, joined!.HouseholdId);
        Assert.Equal("Sam", joined.DisplayName);
    }

    [Fact]
    public async Task Joining_with_an_unknown_join_code_returns_not_found()
    {
        var client = SignedInClient("lonely-family-member");

        var response = await client.PostAsJsonAsync("/api/household/join", new JoinHouseholdRequest("NOPE99", "Sam"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Joining_with_a_lowercase_join_code_still_matches()
    {
        var creator = SignedInClient("case-sensitivity-creator");
        var created = await (await creator.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex")))
            .Content.ReadFromJsonAsync<HouseholdResponse>();

        var joiner = SignedInClient("case-sensitivity-joiner");
        var response = await joiner.PostAsJsonAsync(
            "/api/household/join",
            new JoinHouseholdRequest(created!.JoinCode.ToLowerInvariant(), "Sam"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_household_twice_for_the_same_family_member_returns_conflict()
    {
        var client = SignedInClient("already-onboarded-member");
        await client.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex"));

        var response = await client.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Joining_a_household_when_already_onboarded_returns_conflict()
    {
        var creator = SignedInClient("second-household-creator");
        var created = await (await creator.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Alex")))
            .Content.ReadFromJsonAsync<HouseholdResponse>();

        var client = SignedInClient("already-onboarded-joiner");
        await client.PostAsJsonAsync("/api/household", new CreateHouseholdRequest("Sam"));

        var response = await client.PostAsJsonAsync(
            "/api/household/join",
            new JoinHouseholdRequest(created!.JoinCode, "Sam"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private HttpClient SignedInClient(string googleSubjectId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, googleSubjectId);
        return client;
    }
}
