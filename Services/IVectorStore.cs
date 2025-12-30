using ChatBot.Models;

namespace ChatBot.Services;

/// <summary>
/// Interface for vector storage operations (indexing/writing)
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Store a document chunk with its embedding vector
    /// </summary>
    Task SaveDocumentChunkWithEmbedding(DocumentChunk chunk, float[] embedding);
    
    /// <summary>
    /// Get the total number of vectors stored
    /// </summary>
    Task<int> GetVectorCount();
    
    /// <summary>
    /// Clear all stored vectors and chunks
    /// </summary>
    Task ClearAllVectors();
}
