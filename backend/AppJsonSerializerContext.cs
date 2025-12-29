using System.Text.Json.Serialization;
using ChatBot.Models;
using Microsoft.Extensions.AI;

namespace ChatBot;

[JsonSerializable(typeof(List<DocumentChunk>))]
[JsonSerializable(typeof(DocumentChunk))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(List<ChatMessage>))]
[JsonSerializable(typeof(ChatMessage))]
[JsonSerializable(typeof(ChatRole))]
[JsonSourceGenerationOptions(WriteIndented = false)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
