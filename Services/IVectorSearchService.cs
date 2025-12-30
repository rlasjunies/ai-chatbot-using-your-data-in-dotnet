using ChatBot.Models;

namespace ChatBot.Services;

/// <summary>
/// Interface for vector search implementations (Pinecone, SQLite, etc.)
/// </summary>
public interface IVectorSearchService
{
    Task<List<DocumentChunk>> FindTopKChunks(string query, int k);
    Task<List<DocumentChunk>> FindInDatabase(string query);
}
