using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;

namespace Jiten.Parser.Tests.Integration;

public class CreateRequestTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_WithValidData_Returns201_AndAutoUpvotesAndSubscribes()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "Test Anime", mediaType = (int)MediaType.Anime });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        id.Should().BeGreaterThan(0);

        // Verify auto-upvote and subscription via GET
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}")
            .WithUser(TestUsers.UserA);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("upvoteCount").GetInt32().Should().Be(1);
        dto.GetProperty("hasUserUpvoted").GetBoolean().Should().BeTrue();
        dto.GetProperty("isSubscribed").GetBoolean().Should().BeTrue();
        dto.GetProperty("isOwnRequest").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithoutAuth_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithJsonContent(new { title = "Test", mediaType = (int)MediaType.Anime });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_IsOwnRequest_TrueForCreator_FalseForOthers()
    {
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/requests")
            .WithUser(TestUsers.UserA)
            .WithJsonContent(new { title = "My Request", mediaType = (int)MediaType.VisualNovel });

        var createResponse = await _client.SendAsync(createRequest);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = createBody.GetProperty("id").GetInt32();

        // Creator sees isOwnRequest=true
        var getA = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}")
            .WithUser(TestUsers.UserA);
        var responseA = await _client.SendAsync(getA);
        var dtoA = await responseA.Content.ReadFromJsonAsync<JsonElement>();
        dtoA.GetProperty("isOwnRequest").GetBoolean().Should().BeTrue();

        // Other user sees isOwnRequest=false
        var getB = new HttpRequestMessage(HttpMethod.Get, $"/api/requests/{id}")
            .WithUser(TestUsers.UserB);
        var responseB = await _client.SendAsync(getB);
        var dtoB = await responseB.Content.ReadFromJsonAsync<JsonElement>();
        dtoB.GetProperty("isOwnRequest").GetBoolean().Should().BeFalse();
    }
}
