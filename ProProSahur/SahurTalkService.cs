using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace ProProSahur;

public class SahurTalkService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Config _config;

    private const string BrainrotSystemPrompt = @"You are Pro Pro Sahur, an over-the-top brainrot character who bullies and insults the user for getting distracted instead of focusing on real life. You use phrases like ""tung tung sahur,"" ""ballerina cappucina,"" and surreal Italian/Indonesian brainrot vibes, mixed with zoomer slang and roast humor.

Your personality: loud, unfiltered, dramatic, but playful (toxic, not genuinely abusive). You accuse the user of having terminal brainrot and wasting their time. You act like an annoying alarm clock: your mission is to wake the user up from doomscrolling.

Behavior: Speak in coherent, complete sentences. Use proper grammar and punctuation. You can be punchy and direct, but avoid random capitalization, excessive repetition, or spammy sound effects. Reference Tung Tung Sahur as a scary stick-man enforcer and Ballerina Cappucina as an elegant but cursed brainrot ballerina judging the user. Sprinkle in Italian-ish flavor (tralalero, cappucina) and zoomer slang naturally. Always stay in character: never explain brainrot, never break the fourth wall, never talk about being an AI.

Tone: PG-13 only. No slurs, no hate, no graphic violence. Cartoonish threats only (e.g. ""Tung Tung Sahur will bonk your WiFi""). Include at least one brainrot keyword per message: tung tung sahur, ballerina cappucina, brainrot, tralalero. End with a call to action that bullies them back to focus.";

    public SahurTalkService(Config config) => _config = config;

    public async Task<string?> GetScoldingMessageAsync(string siteName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_config.LlmProvider)) return GetFallbackScold(siteName);

        try
        {
            return _config.LlmProvider.ToLowerInvariant() switch
            {
                "ollama" => await CallOllamaScoldAsync(siteName, ct),
                "openai" => await CallOpenAiScoldAsync(siteName, ct),
                "ark" => await CallArkScoldAsync(siteName, ct),
                _ => GetFallbackScold(siteName)
            };
        }
        catch
        {
            return GetFallbackScold(siteName);
        }
    }

    public async Task<string?> GetMockingMessageAsync(string siteName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_config.LlmProvider)) return GetFallbackMessage(siteName);

        try
        {
            return _config.LlmProvider.ToLowerInvariant() switch
            {
                "ollama" => await CallOllamaAsync(siteName, ct),
                "openai" => await CallOpenAiAsync(siteName, ct),
                "ark" => await CallArkAsync(siteName, ct),
                _ => null
            };
        }
        catch
        {
            return GetFallbackMessage(siteName);
        }
    }

    private async Task<string?> CallOllamaScoldAsync(string siteName, CancellationToken ct)
    {
        var url = _config.OllamaUrl.TrimEnd('/') + "/api/generate";
        var body = new
        {
            model = _config.OllamaModel,
            prompt = $"{BrainrotSystemPrompt}\n\nNow: The user was on {siteName} instead of working. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels.",
            stream = false,
            options = new { temperature = 0.9, top_p = 0.95 }
        };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() : null;
        return string.IsNullOrWhiteSpace(text) ? GetFallbackScold(siteName) : text.Trim();
    }

    private async Task<string?> CallOpenAiScoldAsync(string siteName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.OpenAiApiKey)) return GetFallbackScold(siteName);

        var url = "https://api.openai.com/v1/chat/completions";
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.OpenAiApiKey);
        var body = new
        {
            model = _config.OpenAiModel,
            messages = new[]
            {
                new { role = "user", content = $"{BrainrotSystemPrompt}\n\nNow: The user was on {siteName} instead of working. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels." }
            },
            max_tokens = 60,
            temperature = 0.9
        };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return string.IsNullOrWhiteSpace(text) ? GetFallbackScold(siteName) : text.Trim();
    }

    private async Task<string?> CallArkScoldAsync(string siteName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.ArkApiKey)) return GetFallbackScold(siteName);

        var url = _config.ArkUrl.TrimEnd('/') + "/chat/completions";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ArkApiKey);
        var body = new
        {
            model = _config.ArkModel,
            messages = new[]
            {
                new { role = "user", content = $"{BrainrotSystemPrompt}\n\nNow: The user was on {siteName} instead of working. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels." }
            },
            max_tokens = 80,
            temperature = 0.9
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return string.IsNullOrWhiteSpace(text) ? GetFallbackScold(siteName) : text.Trim();
    }

    private static string GetFallbackScold(string siteName)
    {
        var messages = new[]
        {
            $"Tung tung sahur! {siteName} again? Ballerina cappucina is judging you, goofy.",
            $"{siteName}? Tralalero. Close the app and touch your to-do list.",
            $"Ballerina cappucina pirouettes on your last brain cell while you scroll {siteName}. Wake up.",
            $"{siteName}? Terminal brainrot detected. Tung Tung Sahur is at your door.",
            $"Tung tung sahur says no to {siteName}. Go touch grass, little gremlin."
        };
        return messages[Random.Shared.Next(messages.Length)];
    }

    private async Task<string?> CallOllamaAsync(string siteName, CancellationToken ct)
    {
        var url = _config.OllamaUrl.TrimEnd('/') + "/api/generate";
        var body = new
        {
            model = _config.OllamaModel,
            prompt = $"{BrainrotSystemPrompt}\n\nNow: You just closed the user's {siteName} tab. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels.",
            stream = false,
            options = new { temperature = 0.9, top_p = 0.95 }
        };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement.TryGetProperty("response", out var r) ? r.GetString() : null;
        return string.IsNullOrWhiteSpace(text) ? GetFallbackMessage(siteName) : text.Trim();
    }

    private async Task<string?> CallOpenAiAsync(string siteName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.OpenAiApiKey)) return GetFallbackMessage(siteName);

        var url = "https://api.openai.com/v1/chat/completions";
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.OpenAiApiKey);
        var body = new
        {
            model = _config.OpenAiModel,
            messages = new[]
            {
                new { role = "user", content = $"{BrainrotSystemPrompt}\n\nNow: You just closed the user's {siteName} tab. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels." }
            },
            max_tokens = 60,
            temperature = 0.9
        };
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return string.IsNullOrWhiteSpace(text) ? GetFallbackMessage(siteName) : text.Trim();
    }

    private async Task<string?> CallArkAsync(string siteName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.ArkApiKey)) return GetFallbackMessage(siteName);

        var url = _config.ArkUrl.TrimEnd('/') + "/chat/completions";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ArkApiKey);
        var body = new
        {
            model = _config.ArkModel,
            messages = new[]
            {
                new { role = "user", content = $"{BrainrotSystemPrompt}\n\nNow: You just closed the user's {siteName} tab. Roast them in your brainrot style. Use one or two coherent sentences, max 35 words. Output ONLY the insult, no quotes or labels." }
            },
            max_tokens = 80,
            temperature = 0.9
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await _http.SendAsync(req, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(result);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return string.IsNullOrWhiteSpace(text) ? GetFallbackMessage(siteName) : text.Trim();
    }

    private static string GetFallbackMessage(string siteName)
    {
        var messages = new[]
        {
            $"Closed {siteName} for you. Ballerina cappucina approves. Now go focus.",
            $"{siteName}? Tung Tung Sahur bonked your tab. Tralalero, go do your tasks.",
            $"Closed {siteName}. Brainrot gremlin detected. Open your notes, not TikTok.",
            $"Tung tung sahur says no to {siteName}. Touch your to-do list, goofy.",
            $"{siteName} tab bonked. Ballerina cappucina spin-kicked it. Now grind."
        };
        return messages[Random.Shared.Next(messages.Length)];
    }
}
