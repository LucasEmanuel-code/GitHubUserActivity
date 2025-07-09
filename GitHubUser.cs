using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

public class GitHubEvent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("repo")]
    public RepoContainer? RepoContainer { get; set; }
    
    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; } // Usamos JsonElement para flexibilidade
}

public class RepoContainer
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class GitHubCLI
{
    public static async Task<List<string>> GetUserActivitiesAsync(string username)
    {
        using HttpClient client = new HttpClient();
        string url = $"https://api.github.com/users/{username}/events";
        client.DefaultRequestHeaders.Add("User-Agent", "GitHubActivityCLI/1.0");

        try
        {
            HttpResponseMessage response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erro: {response.StatusCode}");
                return new List<string>();
            }

            string json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var events = JsonSerializer.Deserialize<List<GitHubEvent>>(json, options);

            var activities = new List<string>();
            
            if (events != null)
            {
                foreach (var e in events)
                {
                    if (e == null || e.Type == null || e.RepoContainer == null || e.RepoContainer.Name == null)
                        continue;

                    string activity = e.Type switch
                    {
                        "PushEvent" => $"Pushed {GetPushEventSize(e.Payload)} commits to {e.RepoContainer.Name}",
                        "IssuesEvent" => $"{Capitalize(e.Payload.TryGetProperty("action", out var action) ? action.GetString() ?? "" : "")} issue in {e.RepoContainer.Name}",
                        "WatchEvent" => $"Starred {e.RepoContainer.Name}",
                        "PullRequestEvent" => $"{Capitalize(e.Payload.TryGetProperty("action", out var prAction) ? prAction.GetString() ?? "" : "")} pull request in {e.RepoContainer.Name}",
                        "CreateEvent" => $"Created {(e.Payload.TryGetProperty("ref_type", out var refType) ? refType.GetString() ?? "" : "")} in {e.RepoContainer.Name}",
                        _ => string.Empty
                    };

                    if (!string.IsNullOrWhiteSpace(activity))
                        activities.Add(activity);
                }
            }

            return activities;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro ao processar: {e.Message}");
            return new List<string>();
        }
    }

    private static int GetPushEventSize(JsonElement payload)
    {
        if (payload.TryGetProperty("size", out var size))
            return size.GetInt32();
        return 1; // Default se não encontrar size
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s))
            return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("Uso: github-activity <username>");
            Console.WriteLine("Exemplo: github-activity octocat");
            return;
        }

        string username = args[0].Trim();
        
        try
        {
            var activities = await GitHubCLI.GetUserActivitiesAsync(username);
            
            if (activities == null || activities.Count == 0)
            {
                Console.WriteLine("Nenhuma atividade recente encontrada.");
                return;
            }

            Console.WriteLine($"Atividades recentes de {username}:");
            foreach (var activity in activities)
            {
                Console.WriteLine($"- {activity}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro fatal: {ex.Message}");
        }
    }
}