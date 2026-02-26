using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class SubscriptionTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateRequest(string userId = TestUsers.UserA)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(userId)
            .WithJsonContent(new { title = "Sub Test", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Subscribe_ReturnsSubscribedTrue()
    {
        var id = await CreateRequest();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("subscribed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_ReturnsSubscribedFalse()
    {
        var id = await CreateRequest();

        // Subscribe first
        var sub = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(sub);

        // Unsubscribe
        var unsub = new HttpRequestMessage(HttpMethod.Delete, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(unsub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("subscribed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateSubscribe_IsIdempotent()
    {
        var id = await CreateRequest();

        var sub1 = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(sub1);

        var sub2 = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(sub2);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("subscribed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpvoteAutoSubscribes_ManualUnsubscribeStays_NextUpvoteResubscribes()
    {
        var id = await CreateRequest();

        // UserB upvotes → auto-subscribed
        var upvote = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(upvote);

        // Manually unsubscribe
        var unsub = new HttpRequestMessage(HttpMethod.Delete, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(unsub);

        // Verify unsubscribed
        var check1 = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var checkResp1 = await _client.SendAsync(check1);
        var checkBody1 = await checkResp1.Content.ReadFromJsonAsync<JsonElement>();
        checkBody1.GetProperty("subscribed").GetBoolean().Should().BeFalse();

        // Un-upvote then re-upvote → should re-subscribe
        var toggleOff = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(toggleOff);

        var toggleOn = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/upvote")
            .WithUser(TestUsers.UserB);
        await _client.SendAsync(toggleOn);

        var check2 = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}/subscribe")
            .WithUser(TestUsers.UserB);
        var checkResp2 = await _client.SendAsync(check2);
        var checkBody2 = await checkResp2.Content.ReadFromJsonAsync<JsonElement>();
        checkBody2.GetProperty("subscribed").GetBoolean().Should().BeTrue();
    }
}
