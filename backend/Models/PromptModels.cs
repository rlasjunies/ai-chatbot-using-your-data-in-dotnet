namespace ChatBot.Models;

/// <summary>
/// Summary of a prompt for list view
/// </summary>
public record PromptListItem(
    string Name,
    string Preview,
    DateTime UpdatedAt
);

/// <summary>
/// Full prompt details for editing
/// </summary>
public record PromptDetail(
    string Name,
    string Content,
    DateTime UpdatedAt
);

/// <summary>
/// Request to update a prompt's content
/// </summary>
public record PromptUpdateRequest(
    string Content
);

/// <summary>
/// Response after updating a prompt
/// </summary>
public record PromptUpdateResponse(
    string Message,
    string Name,
    DateTime UpdatedAt
);

/// <summary>
/// Response after resetting prompts
/// </summary>
public record PromptResetResponse(
    string Message,
    int PromptsReset
);

/// <summary>
/// Generic error response
/// </summary>
public record ErrorResponse(
    string Message
);
