namespace ChatBot.Services;

/// <summary>
/// Provides access to system prompts stored in the database.
/// No caching - reads from DB on each access for immediate effect when prompts are updated.
/// </summary>
public class PromptService
{
    private readonly PromptStore _promptStore;

    public PromptService(PromptStore promptStore)
    {
        _promptStore = promptStore;
    }

    public string RagSystemPrompt => _promptStore.GetPrompt("RagSystemPrompt")
        ?? EmbeddedPrompts.RagSystemPrompt;

    public string ChatSystemPrompt => _promptStore.GetPrompt("ChatSystemPrompt")
        ?? EmbeddedPrompts.ChatSystemPrompt;

    public string HydePrompt => _promptStore.GetPrompt("HydePrompt")
        ?? EmbeddedPrompts.HydePrompt;
}