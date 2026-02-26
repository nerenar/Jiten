using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class DeleteRequestTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateRequest(string userId = TestUsers.UserA)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(userId)
            .WithJsonContent(new { title = "Delete Test", mediaType = (int)MediaType.Anime });
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task DeleteOwnRequest_Returns200()
    {
        var id = await CreateRequest(TestUsers.UserA);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/requests/{id}")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify it's gone
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}")
            .WithUser(TestUsers.UserA);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteOtherUsersRequest_Returns403()
    {
        var id = await CreateRequest(TestUsers.UserA);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/requests/{id}")
            .WithUser(TestUsers.UserB);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteRequestWithComments_CascadeCleanup()
    {
        var id = await CreateRequest(TestUsers.UserA);

        // Add a comment
        var commentContent = new MultipartFormDataContent();
        commentContent.Add(new StringContent("Test comment"), "text");
        var commentRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/requests/{id}/comments")
        {
            Content = commentContent
        };
        commentRequest.WithUser(TestUsers.UserB);
        await _client.SendAsync(commentRequest);

        // Delete the request
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/requests/{id}")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(deleteRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
