using ChatBot.Models;
using Pinecone;

namespace ChatBot.Services;

/// <summary>
/// Pinecone vector store implementation for both search and storage
/// </summary>
public class PineconeVectorStore : IVectorSearchService, IVectorStore
{
    private readonly StringEmbeddingGenerator _embeddingGenerator;
    private readonly IndexClient _pineconeIndex;
    private readonly DocumentChunkStore _contentStore;

    public PineconeVectorStore(
        StringEmbeddingGenerator embeddingGenerator,
        IndexClient pineconeIndex,
        DocumentChunkStore contentStore)
    {
        _embeddingGenerator = embeddingGenerator;
        _pineconeIndex = pineconeIndex;
        _contentStore = contentStore;
    }

    // IVectorSearchService implementation
    public async Task<List<DocumentChunk>> FindTopKChunks(string query, int k)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var embeddings = await _embeddingGenerator.GenerateAsync([query],
                new Microsoft.Extensions.AI.EmbeddingGenerationOptions
                {
                    Dimensions = 512
                });

        var vector = embeddings[0].Vector.ToArray();

        var response = await _pineconeIndex.QueryAsync(new QueryRequest
        {
            Vector = vector,
            TopK = (uint)k,
            IncludeMetadata = true
        });

        var matches = (response.Matches ?? []).ToList();
        if (matches.Count == 0)
            return [];

        var ids = matches.Select(m => m.Id!).Where(id => !string.IsNullOrEmpty(id));
        var articles = _contentStore.GetDocumentChunks(ids);

        var scoreById = matches.Where(m => m.Id is not null)
                               .ToDictionary(m => m.Id!, m => m.Score);

        var ordered = articles.OrderByDescending(a => scoreById.GetValueOrDefault(a.Id, 0f))
                              .Take(k)
                              .ToList();

        return ordered;
    }

    public Task<List<DocumentChunk>> FindInDatabase(string query) => FindTopKChunks(query, 5);

    // IVectorStore implementation
    public async Task SaveDocumentChunkWithEmbedding(DocumentChunk chunk, float[] embedding)
    {
        // Store vector in Pinecone
        var vector = new Vector
        {
            Id = chunk.Id,
            Values = embedding,
            Metadata = new Metadata
            {
                { "title", chunk.Title },
                { "section", chunk.Section },
                { "chunk_index", chunk.ChunkIndex }
            }
        };

        await _pineconeIndex.UpsertAsync(new UpsertRequest
        {
            Vectors = [vector]
        });

        // Store content in local SQLite
        _contentStore.SaveDocumentChunk(chunk);
    }

    public async Task<int> GetVectorCount()
    {
        var stats = await _pineconeIndex.DescribeIndexStatsAsync(new DescribeIndexStatsRequest());
        return (int)(stats.TotalVectorCount ?? 0);
    }

    public async Task ClearAllVectors()
    {
        // Note: Pinecone doesn't have a simple "delete all" operation
        // This would require querying all IDs and deleting them
        // For now, throw NotImplementedException
        throw new NotImplementedException("Clearing all vectors in Pinecone requires recreating the index");
    }
}
