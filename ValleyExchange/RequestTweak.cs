using System.Text.Json.Nodes;

namespace ValleyExchange;

/// <summary>
/// Rewrites ValleyTalk's OpenRouter chat request before it's sent (pure, so it can be checked outside the game):
/// room for a reasoning model to think AND answer, and Wafer first (GLM overthinks ValleyTalk's prompt on the cheaper providers).
/// </summary>
public static class RequestTweak
{
    public const int MaxTokens = 4096;
    public static readonly string[] Providers = { "wafer/us", "wafer" };

    public static string Rewrite(string url, string? content)
    {
        if (string.IsNullOrEmpty(content) || !url.Contains("openrouter.ai") || !url.EndsWith("/chat/completions"))
            return content ?? "";
        if (JsonNode.Parse(content) is not JsonObject request)
            return content;
        request["max_tokens"] = Math.Max(request["max_tokens"]?.GetValue<int>() ?? 0, MaxTokens);
        if (request["model"]?.GetValue<string>() is { } model && model.EndsWith(":floor"))
            request["model"] = model[..^":floor".Length]; // ":floor" sorts by price, which would fight the provider order
        request["provider"] = new JsonObject
        {
            ["order"] = new JsonArray(Providers.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
            ["allow_fallbacks"] = true, // if Wafer's down, any provider beats no Olivia
        };
        return request.ToJsonString();
    }
}
