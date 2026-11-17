using HarmonyLib;
using StardewModdingAPI;

namespace ValleyExchange;

/// <summary>Hooks ValleyTalk's outgoing request (its config has no token cap or provider setting) and runs it through <see cref="RequestTweak"/>.</summary>
internal static class ValleyTalkTweaks
{
    private static IMonitor? monitor;
    private static bool warned;

    public static void Apply(string harmonyId, IMonitor log)
    {
        monitor = log;
        var method = AccessTools.Method("ValleyTalk.Platform.NetworkHelper:MakeRequestAsync");
        if (method == null)
        {
            log.Log("Couldn't find ValleyTalk's request method (did ValleyTalk update?). Olivia's requests are left as they are.", LogLevel.Warn);
            return;
        }
        new Harmony(harmonyId).Patch(method, prefix: new HarmonyMethod(typeof(ValleyTalkTweaks), nameof(BeforeRequest)));
        log.Log($"ValleyTalk requests: up to {RequestTweak.MaxTokens} tokens, provider {string.Join(" > ", RequestTweak.Providers)} first.", LogLevel.Info);
    }

    /// <summary>Harmony prefix: parameter names match NetworkHelper.MakeRequestAsync(string url, string content, ...).</summary>
    private static void BeforeRequest(string url, ref string content)
    {
        try
        {
            content = RequestTweak.Rewrite(url, content);
        }
        catch (Exception ex)
        {
            if (!warned)
                monitor?.Log($"Couldn't adjust a ValleyTalk request, sending it unchanged: {ex.Message}", LogLevel.Warn);
            warned = true; // never block Olivia over this
        }
    }
}
