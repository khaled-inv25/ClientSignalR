using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json.Serialization;
using System.Text.Json;

class SignalRConsoleTest
{
    // --- Configuration ---
    private const string BaseUrl = "https://localhost:44306";
    private const string TokenEndpoint = BaseUrl + "/connect/token";
    private const string HubUrl = BaseUrl + "/online-mobile-user";

    private const string ClientId = "Esh3arTech_App";
    private const string Password = "1q2w3E*";

    // --- Models ---
    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }

    public class MessageDto
    {
        public Guid Id { get; set; }
        public string RecipientPhoneNumber { get; set; }
        public string MessageContent { get; set; }
        public string From { get; set; }
        public string AccessUrl { get; set; }
        public DateTime? UrlExpiresAt { get; set; }
    }

    public class MessageModel
    {
        public Guid Id { get; set; }
        public string RecipientPhoneNumber { get; set; }
        public string MessageContent { get; set; }
        public string From { get; set; }
        public string AccessUrl { get; set; }
        public DateTime? UrlExpiresAt { get; set; }
    }

    private static readonly object _consoleLock = new object();

    private static void DisplayMessage(string type, string from, string content, string url, Guid id)
    {
        lock (_consoleLock)
        {
            Console.WriteLine();
            Console.ForegroundColor = type == "PENDING" ? ConsoleColor.Yellow : ConsoleColor.Cyan;
            Console.WriteLine($"[{type}] --------------------------------------------------");
            Console.ResetColor();

            Console.WriteLine($"  From:    {from}");
            Console.WriteLine($"  Content: {content}");
            if (!string.IsNullOrEmpty(url))
            {
                Console.WriteLine($"  URL:     {url}");
            }
            Console.WriteLine($"  ID:      {id}");

            Console.ForegroundColor = type == "PENDING" ? ConsoleColor.Yellow : ConsoleColor.Cyan;
            Console.WriteLine($"------------------------------------------------------------");
            Console.ResetColor();
        }
    }

    private static async Task<string> GetTokenAsync(string UserName)
    {
        using var client = new HttpClient();
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("username", UserName),
            new KeyValuePair<string, string>("password", Password),
            new KeyValuePair<string, string>("scope", "Esh3arTech")
        });

        try
        {
            var response = await client.PostAsync(TokenEndpoint, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);
            return tokenResponse?.AccessToken;
        }
        catch
        {
            return null;
        }
    }

    public static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("=== SignalR Client ===");
        Console.Write("Username: ");
        var user = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(user))
        {
            Console.WriteLine("Username cannot be empty.");
            return;
        }

        var userToken = await GetTokenAsync("967" + user);
        if (string.IsNullOrEmpty(userToken))
        {
            Console.WriteLine("Authentication failed.");
            return;
        }
        else
        {
            Console.WriteLine("User Authenticated successfully.");
        }

        var connection = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(userToken);
            })
            .WithAutomaticReconnect()
            .Build();

        // Real-time message handler
        connection.On<string>("ReceiveMessage", async message =>
        {
            try
            {
                var msg = JsonSerializer.Deserialize<MessageModel>(message);
                if (msg != null)
                {
                    DisplayMessage("LIVE", msg.From, msg.MessageContent, msg.AccessUrl, msg.Id);
                    await connection.InvokeAsync("AcknowledgeMessage", msg.Id);
                }
            }
            catch (JsonException ex)
            {
                lock (_consoleLock) Console.WriteLine($"🚨 Error: {ex.Message}");
            }
        });

        // Pending messages handler
        connection.On<string>("ReceivePendingMessages", async message =>
        {
            try
            {
                var msgs = JsonSerializer.Deserialize<List<MessageDto>>(message);
                if (msgs != null)
                {
                    foreach (var msg in msgs)
                    {
                        DisplayMessage("PENDING", msg.From, msg.MessageContent, msg.AccessUrl, msg.Id);
                        await connection.InvokeAsync("AcknowledgeMessage", msg.Id);
                    }
                }
            }
            catch (JsonException ex)
            {
                lock (_consoleLock) Console.WriteLine($"🚨 Error: {ex.Message}");
            }
        });

        connection.On<string>("ReceiveBroadcastMessage", message =>
        {
            lock (_consoleLock) Console.WriteLine($"\n📢 BROADCAST: {message}");
        });

        try
        {
            await connection.StartAsync();
            Console.WriteLine("\nConnected! Waiting for messages...");
            Console.WriteLine("Press Ctrl+C to exit.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return;
        }

        // Keep the application running without blocking the message loop
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.SetResult(true);
        };

        await tcs.Task;
        await connection.StopAsync();
        Console.WriteLine("Disconnected.");
    }
}
