using System.Text.Json;
using System.Text.Json.Serialization;
using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace ChatBot;

[JsonSerializable(typeof(List<DocumentChunk>))]
[JsonSerializable(typeof(DocumentChunk))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(IList<ChatMessage>))]
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatRole))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(IndexStatsResponse))]
[JsonSerializable(typeof(IndexBuildResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(List<PromptListItem>))]
[JsonSerializable(typeof(PromptListItem))]
[JsonSerializable(typeof(PromptDetail))]
[JsonSerializable(typeof(PromptUpdateRequest))]
[JsonSerializable(typeof(PromptUpdateResponse))]
[JsonSerializable(typeof(PromptResetResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(DateTime))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
