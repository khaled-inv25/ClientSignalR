using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

class SenderConsoleTest
{
    // --- Configuration ---
    //private const string BaseUrl = "https://localhost:7012";
    private const string BaseUrl = "https://localhost:44306";
    private const string MessagesEndpoint = BaseUrl + "/api/app/message/ingestion-send-one-way-message";
    private const string TokenEndpoint = BaseUrl + "/connect/token";

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

    public class MessageModel
    {
        public string RecipientPhoneNumber { get; set; }
        public string MessageContent { get; set; }
        public string Subject { get; set; }
    }

    public class SendMessageResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
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

    private static async Task SendMessageAsync(HttpClient client)
    {
        Console.WriteLine("\n--- New Message ---");
        //Console.Write("Recipient Phone Number: ");
        //string recipient = Console.ReadLine();
        string recipient = "775265496";

        var message = new MessageModel
        {
            RecipientPhoneNumber = recipient,
            MessageContent = "client sender",
            Subject = "default sender from client"
        };

        var jsonMessage = JsonSerializer.Serialize(message);
        var requestContent = new StringContent(jsonMessage, Encoding.UTF8, "application/json");

        try
        {
            Console.WriteLine("Sending message...");
            var response = await client.PostAsync(MessagesEndpoint, requestContent);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var sendMessageResponse = JsonSerializer.Deserialize<SendMessageResponse>(responseJson);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✔ Message sent successfully! Message ID: {sendMessageResponse.Id}");
                Console.ResetColor();

            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✖ Failed to send message. Status: {response.StatusCode}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"🚨 An error occurred: {ex.Message}");
            Console.ResetColor();
        }
        
    }

    private static readonly object _consoleLock = new object();

    public static async Task Main(string[] args)
    {
        Console.Write("Enter username:");
        var username = Console.ReadLine();
        if (string.IsNullOrEmpty(username))
        {
            username = "esh3ar_userA";
        }

        var token = await GetTokenAsync(username);
        if (token == null)
        {
            Console.WriteLine("Failed to obtain access token. Exiting...");
            return;
        }
        Console.WriteLine($"Current user: {username}");
        Console.WriteLine("Access token obtained successfully.");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var stopwatch = Stopwatch.StartNew();
        int counter = 0;
        while (counter < 1000)
        {
            await SendMessageAsync(client);

            //Console.Write("\nSend another message? (y/n): ");
            //string choice = Console.ReadLine()?.ToLower();
            //if (choice != "y")
            //{
            //    break;
            //}
            counter++;
        }
        stopwatch.Stop();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nExiting application. Time[{stopwatch}]");
        Console.ResetColor();
    }
}