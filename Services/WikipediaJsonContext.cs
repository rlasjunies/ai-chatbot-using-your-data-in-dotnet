using System.Text.Json.Serialization;
using ChatBot.Services;

namespace ChatBot.Services;

[JsonSerializable(typeof(WikipediaClient.WikiApiResponse))]
[JsonSerializable(typeof(WikipediaClient.WikiQuery))]
[JsonSerializable(typeof(WikipediaClient.WikiPage))]
[JsonSerializable(typeof(List<WikipediaClient.WikiPage>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class WikipediaJsonContext : JsonSerializerContext
{
}
