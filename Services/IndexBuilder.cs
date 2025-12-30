using Pinecone;
using Microsoft.Extensions.AI;
using System.Collections.Immutable;

namespace ChatBot.Services;

public class IndexBuilder(
    StringEmbeddingGenerator embeddingGenerator,
    IndexClient pineconeIndex,
    WikipediaClient wikipediaClient,
    DocumentChunkStore chunkStore,
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

            var vectors = chunks.Select((chunk, index) => new Vector
            {
                Id = chunk.Id,
                Values = embeddings[index].Vector.ToArray(),
                Metadata = new Metadata
                {
                    { "title", chunk.Title },
                    { "section", chunk.Section },
                    { "chunk_index", chunk.ChunkIndex }
                }
            });

            await pineconeIndex.UpsertAsync(new UpsertRequest
            {
                Vectors = vectors
            });

            foreach (var chunk in chunks)
            {
                chunkStore.SaveDocumentChunk(chunk);
            }

            // If you have rate limit issues with Pinecone (may happen based on your plan) then uncomment this Task.Delay()
            // see https://docs.pinecone.io/reference/api/database-limits#rate-limits
            // await Task.Delay(500);
        }
    }
}
