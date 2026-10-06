using Domain.Models.Meta;
using Domain.Models.Shared.Result;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntegrationServices.Whatsapp;

public sealed class MetaPhoneClient(HttpClient client, WhatsAppApiSettings settings) : IMetaPhoneClient
{
    public async Task<Result<MetaPhoneAccess>> CheckAccessAsync(string wabaId, string phoneNumberId, string accessToken, CancellationToken ct = default)
    {
        if (!Regex.IsMatch(settings.GraphApiVersion ?? "", @"\Av[0-9]{1,3}\.0\z"))
            return new Error("Meta.NotConfigured", "WhatsApp access checks are not configured yet. Contact the platform administrator.", ErrorType.Validation);
        if (!Regex.IsMatch(wabaId, @"\A[0-9]{5,30}\z") || !Regex.IsMatch(phoneNumberId, @"\A[0-9]{5,30}\z") ||
            string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8192 || accessToken.Any(char.IsWhiteSpace))
            return new Error("Meta.InvalidCredentials", "The stored connection details are invalid.", ErrorType.Validation);
        try
        {
            string? after = null;
            var cursors = new HashSet<string>();
            for (var page = 0; page < 10; page++)
            {
                // Fixed host and validated identifiers; never follow Meta's paging.next URL.
                var uri = $"https://graph.facebook.com/{settings.GraphApiVersion}/{wabaId}/phone_numbers?fields=id,display_phone_number,verified_name&limit=100";
                if (after is not null) uri += "&after=" + Uri.EscapeDataString(after);
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return new Error("Meta.AccessDenied", "Meta rejected access. Check the token, permissions and account ownership.", ErrorType.Validation);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return new Error("Meta.RateLimited", "Meta is limiting requests. Try again later.", ErrorType.Failure);
                if (!response.IsSuccessStatusCode) return Unavailable();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return Unavailable();
                foreach (var item in data.EnumerateArray())
                {
                    if (Text(item, "id") != phoneNumberId) continue;
                    var display = Text(item, "display_phone_number");
                    var name = Text(item, "verified_name");
                    if (display is null || display.Length > 64 || (name?.Length ?? 0) > 512) return Unavailable();
                    return new MetaPhoneAccess(phoneNumberId, display, name);
                }
                if (!document.RootElement.TryGetProperty("paging", out var paging) || !paging.TryGetProperty("next", out _))
                    return new Error("Meta.NumberNotFound", "This number was not found in the configured WhatsApp Business Account.", ErrorType.Validation);
                if (!paging.TryGetProperty("cursors", out var cursorData) || (after = Text(cursorData, "after")) is null ||
                    after.Length > 2048 || !cursors.Add(after)) return Unavailable();
            }
            return new Error("Meta.PageLimit", "The account has too many result pages to check automatically.", ErrorType.Failure);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or TaskCanceledException)
        {
            // Raw responses and exception messages may include credentials. Never return them.
            return Unavailable();
        }
    }
    private static string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static Error Unavailable() => new("Meta.Unavailable", "Meta access could not be checked. No connection verification was saved.", ErrorType.Failure);
}
