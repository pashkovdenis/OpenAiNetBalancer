using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;

namespace ChatSimulator
{
    record Message(string role, string content);

    class Agent
    {
        public string Name { get; }
        public List<Message> History { get; } = new();

        public Agent(string name)
        {
            Name = name;
        }

        public async Task<string> SendAndReceiveAsync(HttpClient client, string endpoint)
        {
            const int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var trimmedHistory = TrimHistoryToMaxWords(200);

                    if (trimmedHistory.Count == 0)
                    {
                        Console.WriteLine($"⚠️ [{Name}] Empty message history, injecting default prompt.");
                        trimmedHistory.Add(new Message("user", "Hi, let's begin a conversation."));
                    }
                    var request = new
                    {
                        model = "gpt-4-serina",
                        messages = trimmedHistory,
                        stream = false
                    };

                    var json = JsonSerializer.Serialize(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    content.Headers.Add("X-Agent", Name);

                    var response = await client.PostAsync(endpoint, content);
                    response.EnsureSuccessStatusCode();

                    var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var reply = result.RootElement
                                      .GetProperty("choices")[0]
                                      .GetProperty("message")
                                      .GetProperty("content")
                                      .GetString();

                    History.Add(new Message("assistant", reply));
                    return reply;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [{Name}] Attempt {attempt} failed: {ex.Message}");
                    if (attempt < maxRetries)
                        await Task.Delay(1000 * attempt);
                    else
                        return $"[error after {maxRetries} attempts: {ex.Message}]";
                }
            }

            return "[unreachable]";
        }

        public void AddUserMessage(string content) =>
            History.Add(new Message("user", content));

        private List<Message> TrimHistoryToMaxWords(int maxWords)
        {
            var trimmed = new List<Message>();
            int totalWords = 0;

            foreach (var msg in History.AsEnumerable().Reverse())
            {
                int wordCount = Regex.Matches(msg.content, @"\b\w+\b").Count;
                if (totalWords + wordCount > maxWords)
                    break;

                trimmed.Insert(0, msg);
                totalWords += wordCount;
            }

            return trimmed;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var endpoint = "http://localhost:55497";

            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 100,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            int parallelCount = 4;
            int turnsPerDialogue = 10000;

            Console.WriteLine($"🚀 Starting {parallelCount} parallel dialogues with {turnsPerDialogue} turns each...");

            var tasks = Enumerable.Range(1, parallelCount).Select(async index =>
            {
                var alice = new Agent("Alice");
                var bob = new Agent("Bob");
                var log = new List<string>();

                alice.AddUserMessage("Let's write a sci-fi story about alien archaeologists discovering Earth ruins.");

                for (int i = 0; i < turnsPerDialogue; i++)
                {
                    var aliceReply = await alice.SendAndReceiveAsync(http, endpoint);
                    log.Add($"**Alice:** {aliceReply}");

                    bob.AddUserMessage(aliceReply);
                    var bobReply = await bob.SendAndReceiveAsync(http, endpoint);
                    log.Add($"**Bob:** {bobReply}");

                    alice.AddUserMessage(bobReply);
                }

                var path = $"story_{index}.md";
                await File.WriteAllLinesAsync(path, log);
                Console.WriteLine($"✅ Dialogue {index} complete → {path}");
            });

            await Task.WhenAll(tasks);
            Console.WriteLine("🌌 All dialogues complete.");
        }
    }
}
