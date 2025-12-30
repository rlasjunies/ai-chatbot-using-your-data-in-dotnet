using Microsoft.Extensions.AI;
using ChatBot.Models;

namespace ChatBot.Services;

/// <summary>
/// Decorator for IChatClient that tracks function invocations and sends progress events via SSE
/// This is a simplified version that wraps the entire response process
/// </summary>
public class ProgressTrackingChatClient
{
    private readonly IChatClient _innerClient;
    private readonly Func<ProgressEvent, Task>? _onProgress;

    public ProgressTrackingChatClient(IChatClient innerClient, Func<ProgressEvent, Task>? onProgress = null)
    {
        _innerClient = innerClient;
        _onProgress = onProgress;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Check if there are tools defined - if yes, we'll send detailed progress updates
        var hasTools = options?.Tools != null && options.Tools.Count > 0;
        var llmStartTime = DateTime.UtcNow;

        if (hasTools && _onProgress != null)
        {
            await _onProgress(new StatusEvent(
                "status",
                "🤖 Sending initial query to LLM...",
                llmStartTime,
                "llm_initial"
            ));
        }

        // Call the inner client - this is where the actual LLM time is spent
        var response = await _innerClient.GetResponseAsync(chatMessages, options, cancellationToken);
        var llmEndTime = DateTime.UtcNow;

        // Analyze the response to extract detailed exchange information
        if (hasTools && _onProgress != null)
        {
            var messages = response.Messages.ToList();
            
            // Track if we've seen different types of interactions
            bool foundToolResult = false;
            int toolCallCount = 0;
            var analysisStartTime = llmEndTime;
            
            foreach (var message in messages)
            {
                // Look for tool calls (LLM deciding to use a tool)
                foreach (var content in message.Contents)
                {
                    if (content is FunctionCallContent funcCall)
                    {
                        toolCallCount++;
                        var funcName = funcCall.Name ?? "unknown";
                        var eventTime = analysisStartTime.AddMilliseconds(toolCallCount * 2);
                        
                        // Try to extract the query argument
                        string queryArg = "";
                        if (funcCall.Arguments != null)
                        {
                            var argsDict = funcCall.Arguments as IDictionary<string, object?>;
                            if (argsDict != null && argsDict.TryGetValue("query", out var queryObj))
                            {
                                queryArg = queryObj?.ToString() ?? "";
                            }
                            else
                            {
                                queryArg = funcCall.Arguments.ToString() ?? "";
                            }
                        }
                        
                        var message_text = funcName switch
                        {
                            "database_search_service" => $"🧠 LLM decided to search database with query: \"{queryArg}\"",
                            _ => $"🧠 LLM decided to call tool: {funcName}"
                        };

                        await _onProgress(new FunctionCallEvent(
                            "function_call",
                            message_text,
                            eventTime,
                            funcName,
                            queryArg
                        ));
                    }
                    else if (content is FunctionResultContent funcResult)
                    {
                        foundToolResult = true;
                        var funcName = funcResult.CallId ?? "unknown";
                        var result = funcResult.Result?.ToString() ?? "";
                        var eventTime = analysisStartTime.AddMilliseconds(toolCallCount * 2 + 50);
                        
                        // Try to count the number of results
                        int resultCount = 0;
                        if (result.Contains("landmarks", StringComparison.OrdinalIgnoreCase))
                        {
                            // Rough heuristic to count results
                            resultCount = result.Split(new[] { "\"Title\":" }, StringSplitOptions.None).Length - 1;
                        }
                        
                        var message_text = resultCount > 0
                            ? $"📊 Database returned {resultCount} relevant chunks"
                            : "✅ Database search completed - Found relevant information";

                        await _onProgress(new FunctionResultEvent(
                            "function_result",
                            message_text,
                            eventTime,
                            funcName,
                            result.Length > 150 ? $"{result.Substring(0, 150)}..." : result
                        ));
                    }
                }
            }
            
            // If tools were used, indicate the LLM is processing the results
            // This message should come right before the final synthesis started
            if (foundToolResult)
            {
                await _onProgress(new StatusEvent(
                    "status",
                    "🤖 LLM synthesizing answer from search results...",
                    analysisStartTime.AddMilliseconds(100),
                    "llm_synthesis"
                ));
            }
        }

        return response;
    }
}
