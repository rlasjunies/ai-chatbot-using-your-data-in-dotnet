using ChatBot;
using ChatBot.Services;
using Microsoft.Extensions.AI;

// Load .env file if it exists
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure port from environment variable with default value = 5108
var port = Environment.GetEnvironmentVariable("PORT");
var portNumber = !string.IsNullOrEmpty(port) && int.TryParse(port, out var p) ? p : 5108;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(portNumber);
});

// Configure JSON serialization for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

Startup.ConfigureServices(builder);
var app = builder.Build();

// Uncomment to do indexing when you run the project (you only need to do this once)...
// var indexer = app.Services.GetRequiredService<IndexBuilder>();
// await indexer.BuildIndex(SourceData.LandmarkNames);

// Serve embedded frontend at /ui
app.MapGet("/ui", () => Results.Content(EmbeddedFrontend.IndexHtml, "text/html"));
app.MapGet("/ui/chat", () => Results.Content(EmbeddedFrontend.ChatHtml, "text/html"));
app.MapGet("/ui/question", () => Results.Content(EmbeddedFrontend.QuestionHtml, "text/html"));
app.MapGet("/ui/searchchunks", () => Results.Content(EmbeddedFrontend.SearchChunksHtml, "text/html"));
app.MapGet("/ui/searchlandmarks", () => Results.Content(EmbeddedFrontend.SearchLandmarksHtml, "text/html"));

// API endpoints
// GET /api/search?query=...
app.MapGet("/api/search", async (string query, VectorSearchService search) =>
{
    var results = await search.FindTopKChunks(query, 3);
    return Results.Ok(results);
});

// GET /api/ask?question=...
app.MapGet("/api/ask", async (string question, RagQuestionService rag) =>
{
    var result = await rag.AnswerQuestion(question);
    return Results.Ok(result);
});

// POST /api/chat
app.MapPost("/api/chat", async (
    List<ChatMessage> messages,
    IChatClient client,
    ChatOptions chatOptions,
    PromptService promptService) =>
{
    var withSystemPrompt = (new[] { new ChatMessage(ChatRole.System, promptService.ChatSystemPrompt) })
                            .Concat(messages)
                            .ToList();

    var response = await client.GetResponseAsync(withSystemPrompt, chatOptions);
    return Results.Ok(response.Messages);
});

// Launch browser when application starts
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Urls.FirstOrDefault() ?? $"http://localhost:{portNumber}";
    url = url.Replace("[::]", "localhost"); // Replace wildcard bind with localhost
    var uiUrl = $"{url}/ui";

    try
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = uiUrl,
            UseShellExecute = true
        });
        Console.WriteLine($"Opening browser at {uiUrl}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to open browser: {ex.Message}");
        Console.WriteLine($"Please navigate to {uiUrl} manually.");
    }
});

app.Run();