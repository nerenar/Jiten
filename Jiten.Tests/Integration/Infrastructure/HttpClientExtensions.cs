using System.Net.Http.Json;

namespace Jiten.Parser.Tests.Integration.Infrastructure;

public static class HttpClientExtensions
{
    public static HttpRequestMessage WithUser(this HttpRequestMessage request, string userId)
    {
        request.Headers.Add("X-Test-UserId", userId);
        return request;
    }

    public static HttpRequestMessage WithAdmin(this HttpRequestMessage request)
    {
        request.Headers.Add("X-Test-UserId", TestUsers.Admin);
        request.Headers.Add("X-Test-Role", "Administrator");
        return request;
    }

    public static HttpRequestMessage WithJsonContent<T>(this HttpRequestMessage request, T content)
    {
        request.Content = JsonContent.Create(content);
        return request;
    }
}
