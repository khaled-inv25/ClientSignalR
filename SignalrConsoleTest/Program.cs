using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json.Serialization;
using System.Text.Json;

class SignalRConsoleTest
{
    // --- Configuration ---
    private const string BaseUrl = "https://localhost:44306";
    private const string TokenEndpoint = BaseUrl + "/connect/token";
    private const string HubUrl = BaseUrl + "/online-mobile-user";

    // **IMPORTANT: Replace these with your actual client and user credentials**
    private const string ClientId = "Esh3arTech_App";
    // private const string UserName = "esh3ar_userC"; // User's username
    private const string Password = "1q2w3E*"; // User's password
    // ClientSecret and Scope are omitted from the request as per your requirement.

    // --- Token Response Model ---
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

    // --- Token Retrieval Logic (Simplified) ---
    private static async Task<string> GetTokenAsync(string UserName)
    {
        using var client = new HttpClient();

        // Prepare the request body with only the required parameters
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
            Console.WriteLine($"Requesting token from: {TokenEndpoint}");
            var response = await client.PostAsync(TokenEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Token request failed: {response.StatusCode}");
                Console.WriteLine($"Error details: {errorContent}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

            Console.WriteLine("Token successfully retrieved.");
            return tokenResponse?.AccessToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during token retrieval: {ex.Message}");
            return null;
        }
    }

    // --- Main Application Logic ---
    public static async Task Main(string[] args)
    {
        // 1. Login and get the token
        Console.Write("Username: ");
        var user = Console.ReadLine();
        var userToken = await GetTokenAsync(user!);
        if (string.IsNullOrEmpty(userToken))
        {
            Console.WriteLine("Could not proceed without a valid token.");
            return;
        }

        // 2. Connect to the SignalR Hub using the token
        var connection = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(userToken);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<string>("ReceiveMessage", async message =>
        {
            Console.WriteLine("+---------------+-----------------------------------+");
            Console.WriteLine("| From          | Message Content                   |");
            Console.WriteLine("+---------------+-----------------------------------+");
            MessageModel msg;
            try
            {
                msg = JsonSerializer.Deserialize<MessageModel>(message);
                Console.WriteLine($"| {msg.From,-13} | {msg.MessageContent,-33} |");
                await connection.InvokeAsync("AcknowledgeMessage", msg.Id);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"🚨 ERROR deserializing pending messages: {ex.Message}");
                return;
            }
        });

        connection.On<string>("ReceivePendingMessages", async message =>
        {
            Console.WriteLine("📩 Pending Messages");
            Console.WriteLine("+----------------------+----------------------+--------------------------+");
            Console.WriteLine("| Message Content      | From                 | Access URL               |");
            Console.WriteLine("+----------------------+----------------------+--------------------------+");
            List<MessageDto> msgs;
            try
            {
                msgs = JsonSerializer.Deserialize<List<MessageDto>>(message);
                foreach (var msg in msgs)
                {
                    Console.WriteLine($"| {msg.MessageContent,-20} | {msg.From,-20} | {msg.AccessUrl,-24} |");
                    await connection.InvokeAsync("AcknowledgeMessage", msg.Id);
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"🚨 ERROR deserializing pending messages: {ex.Message}");
                return;
            }
        });

        connection.On<string>("ReceiveBroadcastMessage", message =>
        {
            Console.WriteLine($"📩 Broadcast message: {message}");
        });

        try
        {
            Console.WriteLine($"Connecting to Hub: {HubUrl}");
            await connection.StartAsync();

            Console.WriteLine("Connection started successfully. Listening for messages...\nPress Enter to exit.");
            Console.WriteLine($"🔃 ==================================== 🔃");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
        }

        Console.ReadLine();

        await connection.StopAsync();
    }
}
