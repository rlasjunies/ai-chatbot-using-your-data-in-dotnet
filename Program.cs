using ChatBot;
using ChatBot.Models;
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

DependencyInjectionSetup.ConfigureServices(builder);
var app = builder.Build();

// Initialize prompts database with defaults if empty
var promptStore = app.Services.GetRequiredService<PromptStore>();
if (promptStore.IsEmpty())
{
    promptStore.ResetToDefaults();
    Console.WriteLine("Prompts database initialized with defaults");
}

// API endpoints for index management
// GET /api/index/list - List all indexed record IDs
app.MapGet("/api/index/list", async (IVectorStore vectorStore) =>
{
    try
    {
        var count = await vectorStore.GetVectorCount();
        return Results.Ok(new IndexStatsResponse((uint)count, count > 0));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get index stats: {ex.Message}");
    }
});

// POST /api/index/build - Build the index
app.MapPost("/api/index/build", async (IndexBuilder indexBuilder) =>
{
    try
    {
        await indexBuilder.BuildIndex(SourceData.LandmarkNames);
        return Results.Ok(new IndexBuildResponse("Index built successfully", SourceData.LandmarkNames.Length));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to build index: {ex.Message}");
    }
});

// API endpoints for prompt management
// GET /api/prompts - List all prompts
app.MapGet("/api/prompts", (PromptStore promptStore) =>
{
    var prompts = promptStore.ListAll();
    var items = prompts.Select(p => new PromptListItem(
        p.Name,
        p.Content.Length > 100 ? p.Content.Substring(0, 100) + "..." : p.Content,
        p.UpdatedAt
    )).ToList();
    return Results.Ok(items);
});

// GET /api/prompts/{name} - Get specific prompt
app.MapGet("/api/prompts/{name}", (string name, PromptStore promptStore) =>
{
    var prompts = promptStore.ListAll();
    var prompt = prompts.FirstOrDefault(p => p.Name == name);

    if (prompt.Name == null)
    {
        return Results.NotFound(new ErrorResponse($"Prompt '{name}' not found"));
    }

    return Results.Ok(new PromptDetail(prompt.Name, prompt.Content, prompt.UpdatedAt));
});

// PUT /api/prompts/{name} - Update prompt
app.MapPut("/api/prompts/{name}", (string name, PromptUpdateRequest request, PromptStore promptStore) =>
{
    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest(new ErrorResponse("Content cannot be empty"));
    }

    promptStore.SetPrompt(name, request.Content);
    return Results.Ok(new PromptUpdateResponse(
        "Prompt updated successfully",
        name,
        DateTime.UtcNow
    ));
});

// POST /api/prompts/reset - Reset all prompts to defaults
app.MapPost("/api/prompts/reset", (PromptStore promptStore) =>
{
    promptStore.ResetToDefaults();
    return Results.Ok(new PromptResetResponse(
        "All prompts reset to defaults",
        3
    ));
});

// Redirect root to UI
app.MapGet("/", () => Results.Redirect("/ui"));

// Serve embedded frontend at /ui
app.MapGet("/ui", () => Results.Content(EmbeddedFrontend.IndexHtml, "text/html"));
app.MapGet("/ui/indexer", () => Results.Content(EmbeddedFrontend.IndexerHtml, "text/html"));
app.MapGet("/ui/prompts", () => Results.Content(EmbeddedFrontend.PromptEditorHtml, "text/html"));
app.MapGet("/ui/chat", () => Results.Content(EmbeddedFrontend.ChatHtml, "text/html"));
app.MapGet("/ui/question", () => Results.Content(EmbeddedFrontend.QuestionHtml, "text/html"));
app.MapGet("/ui/searchchunks", () => Results.Content(EmbeddedFrontend.SearchChunksHtml, "text/html"));
app.MapGet("/ui/searchlandmarks", () => Results.Content(EmbeddedFrontend.SearchLandmarksHtml, "text/html"));

// API endpoints
// GET /api/search?query=...
app.MapGet("/api/search", async (string query, IVectorSearchService search) =>
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

// POST /api/chat/stream - Server-Sent Events streaming with progress tracking
app.MapPost("/api/chat/stream", async (
    List<ChatMessage> messages,
    IChatClient client,
    ChatOptions chatOptions,
    PromptService promptService,
    HttpContext context) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    async Task SendEvent(ProgressEvent evt)
    {
        var eventType = evt.GetType();
        var json = System.Text.Json.JsonSerializer.Serialize(evt, eventType, AppJsonSerializerContext.Default);
        await context.Response.WriteAsync($"data: {json}\n\n");
        await context.Response.Body.FlushAsync();
    }

    try
    {
        await SendEvent(new StatusEvent("status", "📨 Processing your request...", DateTime.UtcNow, "started"));

        var withSystemPrompt = (new[] { new ChatMessage(ChatRole.System, promptService.ChatSystemPrompt) })
                                .Concat(messages)
                                .ToList();

        // Extract the user's question for display
        var userQuestion = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        if (!string.IsNullOrEmpty(userQuestion) && userQuestion.Length > 100)
        {
            userQuestion = userQuestion.Substring(0, 100) + "...";
        }
        
        if (!string.IsNullOrEmpty(userQuestion))
        {
            await SendEvent(new StatusEvent("status", $"🔍 Analyzing: \"{userQuestion}\"", DateTime.UtcNow, "analyzing"));
        }

        // Wrap the client with progress tracking to intercept function calls
        var progressClient = new ProgressTrackingChatClient(client, SendEvent);
        var response = await progressClient.GetResponseAsync(withSystemPrompt, chatOptions);

        await SendEvent(new StatusEvent("status", "💭 Response generated successfully", DateTime.UtcNow, "generating"));

        // Extract the messages from the response
        var allMessages = response.Messages.ToList();
        var assistantMessage = allMessages.LastOrDefault(m => m.Role == ChatRole.Assistant);
        var assistantContent = assistantMessage?.Text ?? "";

        Console.WriteLine($"[SSE] Response has {allMessages.Count} total messages");
        Console.WriteLine($"[SSE] Assistant message found: {assistantMessage != null}");
        Console.WriteLine($"[SSE] Assistant content length: {assistantContent.Length}");

        // Send only new messages (system prompt and original messages are already in history)
        var newMessages = allMessages.Where(m => m.Role == ChatRole.Assistant || m.Role == ChatRole.Tool).ToList();

        // Send the complete response
        await SendEvent(new CompletionEvent(
            "completion",
            "✅ Response complete",
            DateTime.UtcNow,
            assistantContent,
            newMessages
        ));

        await context.Response.WriteAsync("data: [DONE]\n\n");
        await context.Response.Body.FlushAsync();
    }
    catch (Exception ex)
    {
        await SendEvent(new StatusEvent("error", $"❌ Error: {ex.Message}", DateTime.UtcNow, "error"));
        await context.Response.WriteAsync("data: [DONE]\n\n");
    }
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