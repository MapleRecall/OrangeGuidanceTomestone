namespace OrangeGuidanceTomestone.Helpers;

internal static class ServerHelper {
    internal static HttpRequestMessage GetRequest(string apiKey, HttpMethod method, string tail, string? contentType = null, HttpContent? content = null) {
        if (!tail.StartsWith('/')) {
            tail = '/' + tail;
        }

        var url = $"https://tryfingerbuthole.anna.lgbt{tail}";
        var req = new HttpRequestMessage(method, url);
        if (content != null) {
            req.Content = content;
        }

        req.Headers.Add("X-Api-Key", apiKey);
        if (contentType != null) {
            req.Headers.Add("Content-Type", contentType);
        }

        return req;
    }

    internal static async Task<HttpResponseMessage> SendRequest(string apiKey, HttpMethod method, string tail, string? contentType = null, HttpContent? content = null) {
        var req = GetRequest(apiKey, method, tail, contentType, content);
        return await new HttpClient().SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
    }
}
