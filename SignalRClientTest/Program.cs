using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

class SignalRConsoleTest
{
    // --- Configuration ---
    //private const string BaseUrl = "https://localhost:7012";
    private const string BaseUrl = "https://localhost:44306";
    private const string TokenEndpoint = BaseUrl + "/connect/token";
    private const string HubUrl = BaseUrl + "/online-mobile-user";
    private const string BusinessHubUrl = BaseUrl + "/business-user";

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
        public Guid Id { get; set; }
        public Guid CreatorId { get; set; }
        public string RecipientPhoneNumber { get; set; }
        public string MessageContent { get; set; }
        public string From { get; set; }
        public string AccessUrl { get; set; }
        public DateTime? UrlExpiresAt { get; set; }
    }

    public class SendMobileToBusinessMessage
    {
        public Guid Id { get; set; }

        public string From { get; set; }

        public string MobileAccount { get; set; }

        public string Content { get; set; }
    }

    public class SendUserToMobileMessage
    {
        public Guid SenderId { get; set; }

        public Guid MessageId { get; set; }

        public string ReceipientMobileNumber { get; set; }

        public string MobileAccount { get; set; }

        public string Content { get; set; }
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

    private static string? GetUserIdFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // ABP usually uses one of these claim types
        return jwt.Claims.FirstOrDefault(c =>
               c.Type == "sub" ||
               c.Type == "user_id" ||
               c.Type == "userid" ||
               c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
           )?.Value;
    }

    public static async Task Main(string[] args)
    {
        int msgChatCounter = 0;
        Console.Clear();
        Console.WriteLine("=== SignalR Client ===");
        Console.Write("Username: ");
        var user = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(user))
        {
            Console.WriteLine("Username cannot be empty.");
            return;
        }


        var userToken = user.StartsWith('7') ? await GetTokenAsync("967" + user) : await GetTokenAsync(user);
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
            .WithUrl(user.StartsWith('7') ? HubUrl : BusinessHubUrl, options =>
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
                    DisplayMessage("LIVE", msg.CreatorId.ToString(), msg.MessageContent, msg.AccessUrl, msg.Id);
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
                var msgs = JsonSerializer.Deserialize<List<MessageModel>>(message);
                if (msgs != null)
                {
                    foreach (var msg in msgs)
                    {
                        DisplayMessage("PENDING", msg.CreatorId.ToString(), msg.MessageContent, msg.AccessUrl, msg.Id);
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

        // Real-time chat
        connection.On<string>("ReceiveChatMessage", message =>
        {
            try
            {
                if (!user.StartsWith('7'))
                {
                    var msg = JsonSerializer.Deserialize<SendMobileToBusinessMessage>(message);
                    DisplayMessage($"Chat: {msgChatCounter}", msg.From, msg.Content, "Null", msg.Id);
                }
                else
                {
                    var msg = JsonSerializer.Deserialize<SendUserToMobileMessage>(message);
                    DisplayMessage($"Chat: {msgChatCounter}", msg.SenderId.ToString(), msg.Content, "Null", new Guid());
                }
                msgChatCounter++;
            }
            catch (JsonException ex)
            {
                lock (_consoleLock) Console.WriteLine($"🚨 Error: {ex.Message}");
            }
        });

        try
        {
            await connection.StartAsync();
            var userId = GetUserIdFromToken(userToken);

            if (!user.StartsWith('7'))
            {
                while (true)
                {
                    Console.WriteLine("\"Enter\" message to send, or\\n to Stop: ");
                    var input = Console.ReadLine();
                    if (input != null && input.Trim().Equals("n", StringComparison.CurrentCultureIgnoreCase))
                    {
                        break;
                    }

                    var message = new SendUserToMobileMessage
                    {
                        SenderId = Guid.Parse(userId),
                        MessageId = Guid.NewGuid(),
                        Content = input,
                        MobileAccount = "775265494",
                        ReceipientMobileNumber = "775265496"
                    };

                    await connection.InvokeAsync("SendMessage", message);
                }
            }
            else
            {
                while (true)
                {
                    Console.WriteLine("\"Enter\" service code or text, or\\n to Stop: ");
                    var input = Console.ReadLine();
                    if (input != null && input.Trim().Equals("n", StringComparison.CurrentCultureIgnoreCase))
                    {
                        break;
                    }
                    var message = new SendMobileToBusinessMessage
                    {
                        Id = Guid.Parse(userId),
                        From = user,
                        MobileAccount = "775265494",
                        Content = input
                    };

                    await connection.InvokeAsync("SendMessage", message);
                }

            }

                //Console.WriteLine("\nConnected! Waiting for messages...");
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