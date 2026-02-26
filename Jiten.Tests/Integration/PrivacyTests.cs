using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class PrivacyTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateRequest(string userId = TestUsers.UserA)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(userId)
            .WithJsonContent(new { title = "Privacy Test", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task GetRequests_NeverContainsRequesterId()
    {
        await CreateRequest();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/requests")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().NotContain("requesterId");
        rawJson.Should().NotContain("RequesterId");
    }

    [Fact]
    public async Task GetRequest_ContainsIsOwnRequest_ButNoRequesterId()
    {
        var id = await CreateRequest();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().Contain("isOwnRequest");
        rawJson.Should().NotContain("requesterId");
        rawJson.Should().NotContain("RequesterId");
    }

    [Fact]
    public async Task GetComments_ContainsRoleAndIsOwnComment_ButNoUserId()
    {
        var id = await CreateRequest();

        // Add a comment
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Test"), "text");
        var commentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/comments")
        {
            Content = content
        };
        commentRequest.WithUser(TestUsers.UserA);
        await _client.SendAsync(commentRequest);

        // Get comments as a different user
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}/comments")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(getRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.Should().Contain("role");
        rawJson.Should().Contain("isOwnComment");
        rawJson.Should().NotContain("userId");
    }
}
