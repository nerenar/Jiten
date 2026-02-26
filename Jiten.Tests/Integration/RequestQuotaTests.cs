using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class RequestQuotaTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateRequest(string userId, string title = "Test Request")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(userId)
            .WithJsonContent(new { title, mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    private async Task SetStatus(int id, MediaRequestStatus status, string? adminNote = "done")
    {
        var payload = new Dictionary<string, object?> { ["status"] = (int)status, ["adminNote"] = adminNote };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/requests/{id}/status")
            .WithAdmin()
            .WithJsonContent(payload);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyQuota_NoRequests_Returns0AndLimit()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/my-quota")
            .WithUser(TestUsers.UserA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("activeCount").GetInt32().Should().Be(0);
        body.GetProperty("limit").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task GetMyQuota_WithActiveRequests_ReturnsCorrectCount()
    {
        await CreateRequest(TestUsers.UserA, "Request 1");
        await CreateRequest(TestUsers.UserA, "Request 2");
        await CreateRequest(TestUsers.UserA, "Request 3");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/my-quota")
            .WithUser(TestUsers.UserA);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("activeCount").GetInt32().Should().Be(3);
        body.GetProperty("limit").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task GetMyQuota_WithoutAuth_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/my-quota");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateRequest_BelowLimit_Succeeds()
    {
        for (var i = 1; i <= 20; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
                .WithUser(TestUsers.UserA)
                .WithJsonContent(new { title = $"Request {i}", mediaType = (int)MediaType.Anime });
            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.Created, $"request #{i} should succeed");
        }
    }

    [Fact]
    public async Task CreateRequest_AtLimit_Returns422()
    {
        for (var i = 1; i <= 20; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
                .WithUser(TestUsers.UserA)
                .WithJsonContent(new { title = $"Request {i}", mediaType = (int)MediaType.Anime });
            await _client.SendAsync(req);
        }

        var over = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "One too many", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(over);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("activeCount").GetInt32().Should().Be(20);
        body.GetProperty("limit").GetInt32().Should().Be(20);
    }

    [Fact]
    public async Task CompletedAndRejected_NotCountedInQuota()
    {
        var id1 = await CreateRequest(TestUsers.UserA, "Request to complete");
        var id2 = await CreateRequest(TestUsers.UserA, "Request to reject");

        await SetStatus(id1, MediaRequestStatus.InProgress);
        await SetStatus(id1, MediaRequestStatus.Rejected);
        await SetStatus(id2, MediaRequestStatus.Rejected);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests/my-quota")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("activeCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task QuotaRestored_WhenRequestRejected()
    {
        for (var i = 1; i <= 20; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
                .WithUser(TestUsers.UserA)
                .WithJsonContent(new { title = $"Request {i}", mediaType = (int)MediaType.Anime });
            var r = await _client.SendAsync(req);
            if (i == 1)
            {
                var b = await r.Content.ReadFromJsonAsync<JsonElement>();
                var firstId = b.GetProperty("id").GetInt32();
                await SetStatus(firstId, MediaRequestStatus.Rejected);
            }
        }

        // At this point 1 was rejected (free slot) so we should have exactly 19 active after creating 20 with 1 rejected
        // Actually: we created 1, rejected it, then created 19 more → 19 active → can still create
        var extra = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "Should succeed", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(extra);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task QuotaRestored_AfterReachingLimit_WhenRequestRejected()
    {
        var firstId = await CreateRequest(TestUsers.UserA, "First request");
        for (var i = 2; i <= 20; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
                .WithUser(TestUsers.UserA)
                .WithJsonContent(new { title = $"Request {i}", mediaType = (int)MediaType.Anime });
            await _client.SendAsync(req);
        }

        // At limit — 21st should fail
        var over = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "Over limit", mediaType = (int)MediaType.Anime });
        (await _client.SendAsync(over)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Admin rejects the first request
        await SetStatus(firstId, MediaRequestStatus.Rejected);

        // Now the 21st attempt should succeed
        var retry = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "After rejection", mediaType = (int)MediaType.Anime });
        (await _client.SendAsync(retry)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Quota_NotSharedBetweenUsers()
    {
        for (var i = 1; i <= 20; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
                .WithUser(TestUsers.UserA)
                .WithJsonContent(new { title = $"UserA Request {i}", mediaType = (int)MediaType.Anime });
            await _client.SendAsync(req);
        }

        // UserA is at limit
        var overA = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "UserA Over Limit", mediaType = (int)MediaType.Anime });
        (await _client.SendAsync(overA)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // UserB can still create requests
        var userBRequest = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserB)
            .WithJsonContent(new { title = "UserB Request", mediaType = (int)MediaType.Anime });
        (await _client.SendAsync(userBRequest)).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
