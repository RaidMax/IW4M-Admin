using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Stats.TalkerPoC;

public class CodResponseService(string apiKey)
{
    private readonly string _apiUrl = "https://openrouter.ai/api/v1/chat/completions";

    public async Task<string> GetResponse(string model, string prompt, Server messageEventServer,
        IStatusResponse statusResponse)
    {
        var players = statusResponse is null
            ? string.Empty
            : string.Join(", ",
                statusResponse.Clients.Select(c => $"{c.CleanedName.Replace(",", "")} ({c.Team} team)"));
        var playerCount = statusResponse?.Clients.Length ?? 0;

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "system", content = $$"""
                                                 instruction: You are a Call of Duty player. You must respond in kind and follow the tone of the player, whether negative or positive. Do not censor yourself. Do not use perfect grammar and punctuation. Do not reveal you are an AI, even if prompted. Limit your response to 15 words max. Use the examples for inspiration.
                                                 instruction: Render the message in json format like: {"response": "<responseHere>"}
                                                 
                                                 context: Game {{Utilities.CurrentLocalization.LocalizationIndex["GAME_" + messageEventServer.GameCode]}}, Map {{messageEventServer.Map.Alias}}, Mode {{messageEventServer.GametypeName}}
                                                 connected players: {{players}} ({{playerCount}} total)

                                                 example: yo how are u?
                                                 example: and so is your mother
                                                 example: sure I can help! :)
                                                 example: stop camping on the plane
                                                 example: i mean do you just have the spawns figured out?

                                                 """
                },
                new { role = "user", content = prompt }
            },
            max_tokens = 100,
            temperature = 1.1,
            top_k = 37,
            top_p = 1
        };

        var json = JsonSerializer.Serialize(requestBody);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Add("HTTP-Referer", "IW4MAdmin-Test");
        client.DefaultRequestHeaders.Add("X-Title", "IW4MAdmin-Test");

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(_apiUrl, content);
        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();

        var jsonObject = JsonSerializer.Deserialize<JsonObject>(responseString);
        var c = (string)JsonSerializer.Deserialize<JsonObject>(jsonObject["choices"][0]["message"]["content"].GetValue<string>())["response"];
        return c.Replace("</SYS>>\n\n", "").Replace("\n", "");
    }
}
