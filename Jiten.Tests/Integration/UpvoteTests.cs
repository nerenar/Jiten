using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class UpvoteTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateRequest(string userId = TestUsers.UserA)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(userId)
            .WithJsonContent(new { title = "Upvote Test", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task ToggleUpvote_On_IncreasesCount_AndAutoSubscribes()
    {
        var id = await CreateRequest();

        // UserB upvotes
        var upvoteRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(upvoteRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("upvoted").GetBoolean().Should().BeTrue();
        body.GetProperty("upvoteCount").GetInt32().Should().Be(2); // 1 auto + 1 UserB

        // Verify auto-subscription
        var subCheck = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var subResponse = await _client.SendAsync(subCheck);
        var subBody = await subResponse.Content.ReadFromJsonAsync<JsonElement>();
        subBody.GetProperty("subscribed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ToggleUpvote_Off_DecreasesCount()
    {
        var id = await CreateRequest();

        // UserB upvotes then un-upvotes
        var upvoteOn = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(upvoteOn);

        var upvoteOff = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(upvoteOff);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("upvoted").GetBoolean().Should().BeFalse();
        body.GetProperty("upvoteCount").GetInt32().Should().Be(1); // Back to just auto-upvote
    }

    [Fact]
    public async Task MultiUserUpvotes_CountReflectsAll()
    {
        var id = await CreateRequest();

        var upvoteB = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(upvoteB);

        var upvoteAdmin = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithAdmin();
        var response = await _client.SendAsync(upvoteAdmin);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("upvoteCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task Upvote_NonExistentRequest_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests/99999/upvote")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
