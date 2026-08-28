using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SessionHandler.Dtos;

namespace SessionHandler.Tests.Infrastructure;

/// <summary>
/// Shared plumbing for the HTTP-level tests: JSON options that match the API's
/// (camelCase, string enums) and thin helpers for the calls every test makes. Tests
/// assert against status codes and response bodies only — never against services or
/// the DbContext directly.
/// </summary>
public abstract class ApiTestBase(HttpClient client)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected HttpClient Client { get; } = client;

    /// <summary>A value unique to one test, for building identity triples that don't collide.</summary>
    protected static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    protected static async Task<T> ReadAs<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(raw, Json)
               ?? throw new InvalidOperationException($"Response body was null: {raw}");
    }

    // --- session lifecycle -------------------------------------------------

    protected Task<HttpResponseMessage> PostLogin(LoginEvent login) =>
        Client.PostAsync("/sessions", Body(login));

    protected Task<HttpResponseMessage> PutUpdate(
        string tenantId, string username, string ip, UpdateSessionRequest request) =>
        Client.PutAsync($"/sessions/{tenantId}/{username}/{ip}", Body(request));

    protected Task<HttpResponseMessage> DeleteLogout(
        string tenantId, string username, string ip, LogoutSessionRequest request) =>
        Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/sessions/{tenantId}/{username}/{ip}")
        {
            Content = Body(request),
        });

    protected Task<HttpResponseMessage> GetSession(int id) =>
        Client.GetAsync($"/sessions/{id}");

    /// <summary>Logs in and asserts it succeeded, returning the created session.</summary>
    protected async Task<SessionResponse> LoginOk(LoginEvent login)
    {
        var response = await PostLogin(login);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAs<SessionResponse>(response);
    }

    // --- search ----------------------------------------------------------

    protected async Task<List<SessionResponse>> SearchSessions(SessionQuery query)
    {
        var response = await Client.PostAsync("/sessions/search", Body(query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAs<List<SessionResponse>>(response);
    }

    protected async Task<List<SessionEventResponse>> SearchEvents(SessionEventQuery query)
    {
        var response = await Client.PostAsync("/session-events/search", Body(query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAs<List<SessionEventResponse>>(response);
    }
}
