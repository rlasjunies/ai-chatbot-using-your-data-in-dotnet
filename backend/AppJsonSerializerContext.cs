using System.Text.Json.Serialization;
using ChatBot.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace ChatBot;

[JsonSerializable(typeof(List<DocumentChunk>))]
[JsonSerializable(typeof(DocumentChunk))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatRole))]
[JsonSerializable(typeof(IndexStatsResponse))]
[JsonSerializable(typeof(IndexBuildResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
