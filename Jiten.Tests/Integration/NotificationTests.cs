using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jiten.Core;
using Jiten.Core.Data;
using Jiten.Parser.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Jiten.Parser.Tests.Integration;

public class NotificationTests(JitenWebApplicationFactory factory)
    : IClassFixture<JitenWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public Task InitializeAsync() => factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedNotification(string userId, string title = "Test notification", bool isRead = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JitenDbContext>();
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = NotificationType.General,
            Title = title,
            Message = "Test message",
            IsRead = isRead,
            ReadAt = isRead ? DateTime.UtcNow : null
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ListNotifications_ReturnsPaginated_NewestFirst()
    {
        await SeedNotification(TestUsers.UserA, "First");
        await Task.Delay(10);
        await SeedNotification(TestUsers.UserA, "Second");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        data.GetArrayLength().Should().Be(2);
        data[0].GetProperty("title").GetString().Should().Be("Second");
        data[1].GetProperty("title").GetString().Should().Be("First");
    }

    [Fact]
    public async Task UnreadCount_ReflectsActualUnread()
    {
        await SeedNotification(TestUsers.UserA, isRead: false);
        await SeedNotification(TestUsers.UserA, isRead: true);
        await SeedNotification(TestUsers.UserA, isRead: false);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/unread-count")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task MarkAsRead_SetsIsReadAndReadAt()
    {
        await SeedNotification(TestUsers.UserA);

        // Get the notification ID
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var listResponse = await _client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var notifId = listBody.GetProperty("data")[0].GetProperty("id").GetInt32();

        // Mark as read
        var readRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/notifications/{notifId}/read")
            .WithUser(TestUsers.UserA);
        var readResponse = await _client.SendAsync(readRequest);
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify
        var verifyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var verifyResponse = await _client.SendAsync(verifyRequest);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var notif = verifyBody.GetProperty("data")[0];
        notif.GetProperty("isRead").GetBoolean().Should().BeTrue();
        notif.GetProperty("readAt").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAllAsRead_MarksAll()
    {
        await SeedNotification(TestUsers.UserA);
        await SeedNotification(TestUsers.UserA);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/read-all")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify unread count is 0
        var countRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/unread-count")
            .WithUser(TestUsers.UserA);
        var countResponse = await _client.SendAsync(countRequest);
        var countBody = await countResponse.Content.ReadFromJsonAsync<JsonElement>();
        countBody.GetProperty("count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DeleteNotification_RemovesFromList()
    {
        await SeedNotification(TestUsers.UserA);

        // Get ID
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var listResponse = await _client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var notifId = listBody.GetProperty("data")[0].GetProperty("id").GetInt32();

        // Delete
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/notifications/{notifId}")
            .WithUser(TestUsers.UserA);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify gone
        var verifyRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var verifyResponse = await _client.SendAsync(verifyRequest);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        verifyBody.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task MarkAsRead_Idempotent()
    {
        await SeedNotification(TestUsers.UserA);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/notifications")
            .WithUser(TestUsers.UserA);
        var listResponse = await _client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var notifId = listBody.GetProperty("data")[0].GetProperty("id").GetInt32();

        // Mark as read twice
        var r1 = new HttpRequestMessage(HttpMethod.Post, $"/api/notifications/{notifId}/read")
            .WithUser(TestUsers.UserA);
        await _client.SendAsync(r1);

        var r2 = new HttpRequestMessage(HttpMethod.Post, $"/api/notifications/{notifId}/read")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(r2);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteNotification_Idempotent()
    {
        // Delete non-existent notification
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/notifications/99999")
            .WithUser(TestUsers.UserA);
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
