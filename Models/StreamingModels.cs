using Microsoft.Extensions.AI;

namespace ChatBot.Models;

/// <summary>
/// Base class for SSE progress events
/// </summary>
public record ProgressEvent(
    string Type,
    string Message,
    DateTime Timestamp
);

/// <summary>
/// Event when a function/tool is about to be called
/// </summary>
public record FunctionCallEvent(
    string Type,
    string Message,
    DateTime Timestamp,
    string FunctionName,
    string? Arguments
) : ProgressEvent(Type, Message, Timestamp);

/// <summary>
/// Event when a function/tool completes
/// </summary>
public record FunctionResultEvent(
    string Type,
    string Message,
    DateTime Timestamp,
    string FunctionName,
    string? Result
) : ProgressEvent(Type, Message, Timestamp);

/// <summary>
/// Event for general status updates
/// </summary>
public record StatusEvent(
    string Type,
    string Message,
    DateTime Timestamp,
    string Status
) : ProgressEvent(Type, Message, Timestamp);

/// <summary>
/// Final response event containing the complete answer
/// </summary>
public record CompletionEvent(
    string Type,
    string Message,
    DateTime Timestamp,
    string Content,
    List<ChatMessage> Messages
) : ProgressEvent(Type, Message, Timestamp);
