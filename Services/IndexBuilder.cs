using Microsoft.Extensions.AI;
using System.Collections.Immutable;

namespace ChatBot.Services;

/// <summary>
/// Builds the vector index by fetching Wikipedia articles and storing them
/// Works with any IVectorStore implementation (Pinecone, SQLite-vec, etc.)
/// </summary>
public class IndexBuilder(
    StringEmbeddingGenerator embeddingGenerator,
    IVectorStore vectorStore,
    WikipediaClient wikipediaClient,
    ArticleSplitter splitter)
{
    public async Task BuildIndex(string[] pageTitles)
    {
        foreach (var title in pageTitles)
        {
            // Swap out the wikipediaClient here to connect to your own data source!
            var page = await wikipediaClient.GetWikipediaPageForTitle(title, full: true);
            var sections = wikipediaClient.SplitIntoSections(page.Content);

            var chunks = sections.SelectMany(section =>
                        splitter.Chunk(page.Title, section.Content, page.PageUrl, section.Title))
                        .Take(25)
                        .ToImmutableList();

            var stringsToEmbed = chunks.Select(c => $"{c.Title} > {c.Section}\n\n{c.Content}");

            // Makes a call to OpenAI to create an embedding from these strings
            var embeddings = await embeddingGenerator.GenerateAsync(
                stringsToEmbed,
                new EmbeddingGenerationOptions { Dimensions = 512 }
            );

            // Store chunks with their embeddings using the configured vector store
            for (int i = 0; i < chunks.Count; i++)
            {
                await vectorStore.SaveDocumentChunkWithEmbedding(
                    chunks[i],
                    embeddings[i].Vector.ToArray()
                );
            }

            Console.WriteLine($"Indexed {chunks.Count} chunks for '{title}'");
        }
    }
}
