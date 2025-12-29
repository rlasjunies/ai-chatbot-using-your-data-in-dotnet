using System.Text.Json;
using ChatBot.Services;
using Microsoft.Extensions.AI;

public static class FunctionRegistry
{
    public static IEnumerable<AITool> GetTools(this IServiceProvider sp, JsonSerializerOptions? jsonOptions = null)
    {
        // This code happens in the composition root, so pull the service from the IServiceProvider
        var vectorService = sp.GetRequiredService<VectorSearchService>();

        // AOT-compatible: Use delegate instead of reflection
        yield return AIFunctionFactory.Create(
            (string query) => vectorService.FindInDatabase(query),
            "database_search_service",
            "Searches for information about landmarks in the database based on a semantic search query.",
            jsonOptions);

    }
}

