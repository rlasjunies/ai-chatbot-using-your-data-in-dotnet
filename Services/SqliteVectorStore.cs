using ChatBot.Models;
using Microsoft.Data.Sqlite;
using System.Text;

namespace ChatBot.Services;

/// <summary>
/// Vector search implementation using SQLite with sqlite-vec extension
/// This stores both embeddings and document content in a single SQLite database
/// </summary>
public class SqliteVectorStore : IVectorSearchService, IVectorStore
{
    private const string DbFile = "VectorStore.db";
    private readonly StringEmbeddingGenerator _embeddingGenerator;

    public SqliteVectorStore(StringEmbeddingGenerator embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
        InitializeDatabase();
    }

    private static void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={DbFile}");
        conn.Open();

        // Load sqlite-vec extension
        try
        {
            conn.LoadExtension("vec0");
        }
        catch (Exception ex)
        {
            Console.WriteLine("=" + new string('=', 70));
            Console.WriteLine("ERROR: Failed to load sqlite-vec extension!");
            Console.WriteLine("=" + new string('=', 70));
            Console.WriteLine();
            Console.WriteLine("To use SQLite vector storage, you need the sqlite-vec extension.");
            Console.WriteLine();
            Console.WriteLine("Download instructions:");
            Console.WriteLine("1. Go to: https://github.com/asg017/sqlite-vec/releases");
            Console.WriteLine("2. Download the latest release for your platform:");
            Console.WriteLine("   - Windows: sqlite-vec-*-windows-x86_64.zip");
            Console.WriteLine("   - Linux: sqlite-vec-*-linux-x86_64.tar.gz");
            Console.WriteLine("   - macOS: sqlite-vec-*-macos-x86_64.tar.gz");
            Console.WriteLine("3. Extract the archive and find:");
            Console.WriteLine("   - Windows: vec0.dll");
            Console.WriteLine("   - Linux: vec0.so");
            Console.WriteLine("   - macOS: vec0.dylib");
            Console.WriteLine("4. Copy the file to this application's directory or:");
            Console.WriteLine($"   {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine();
            Console.WriteLine("Alternative: Switch to Pinecone provider in appsettings.json:");
            Console.WriteLine("  \"VectorStore\": { \"Provider\": \"Pinecone\" }");
            Console.WriteLine();
            Console.WriteLine("=" + new string('=', 70));
            throw new InvalidOperationException(
                "sqlite-vec extension not found. See console output for installation instructions.", ex);
        }

        using var cmd = conn.CreateCommand();
        
        // Create table for document chunks with embeddings
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS VectorChunks(
              Id TEXT PRIMARY KEY,
              Title TEXT NOT NULL,
              Section TEXT NOT NULL,
              ChunkIndex INTEGER NOT NULL,
              Content TEXT NOT NULL,
              SourcePageUrl TEXT NOT NULL,
              Embedding BLOB NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_title ON VectorChunks(Title);
        ";
        cmd.ExecuteNonQuery();
    }

    public async Task<List<DocumentChunk>> FindTopKChunks(string query, int k)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        // Generate embedding for the query
        var embeddings = await _embeddingGenerator.GenerateAsync([query],
                new Microsoft.Extensions.AI.EmbeddingGenerationOptions
                {
                    Dimensions = 512
                });

        var queryVector = embeddings[0].Vector.ToArray();
        var queryVectorBlob = SerializeVector(queryVector);

        using var conn = new SqliteConnection($"Data Source={DbFile}");
        conn.Open();
        conn.LoadExtension("vec0");

        using var cmd = conn.CreateCommand();
        
        // Use vec_distance_cosine for similarity search
        // Lower distance = more similar
        cmd.CommandText = @"
            SELECT 
                Id, 
                Title, 
                Section, 
                ChunkIndex, 
                Content, 
                SourcePageUrl,
                vec_distance_cosine(Embedding, @queryVector) as distance
            FROM VectorChunks
            WHERE Embedding IS NOT NULL
            ORDER BY distance ASC
            LIMIT @k
        ";

        cmd.Parameters.AddWithValue("@queryVector", queryVectorBlob);
        cmd.Parameters.AddWithValue("@k", k);

        var results = new List<DocumentChunk>();
        using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            results.Add(new DocumentChunk(
                Id: reader.GetString(0),
                Title: reader.GetString(1),
                Section: reader.GetString(2),
                ChunkIndex: reader.GetInt32(3),
                Content: reader.GetString(4),
                SourcePageUrl: reader.GetString(5)
            ));
        }

        return results;
    }

    public Task<List<DocumentChunk>> FindInDatabase(string query) => FindTopKChunks(query, 5);

    public async Task SaveDocumentChunkWithEmbedding(DocumentChunk chunk, float[] embedding)
    {
        using var conn = new SqliteConnection($"Data Source={DbFile}");
        conn.Open();
        conn.LoadExtension("vec0");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO VectorChunks
            (Id, Title, Section, ChunkIndex, Content, SourcePageUrl, Embedding)
            VALUES (@id, @title, @section, @chunkIndex, @content, @sourcePageUrl, @embedding)
        ";

        cmd.Parameters.AddWithValue("@id", chunk.Id);
        cmd.Parameters.AddWithValue("@title", chunk.Title);
        cmd.Parameters.AddWithValue("@section", chunk.Section);
        cmd.Parameters.AddWithValue("@chunkIndex", chunk.ChunkIndex);
        cmd.Parameters.AddWithValue("@content", chunk.Content);
        cmd.Parameters.AddWithValue("@sourcePageUrl", chunk.SourcePageUrl);
        cmd.Parameters.AddWithValue("@embedding", SerializeVector(embedding));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetVectorCount()
    {
        using var conn = new SqliteConnection($"Data Source={DbFile}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM VectorChunks";
        
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task ClearAllVectors()
    {
        using var conn = new SqliteConnection($"Data Source={DbFile}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM VectorChunks";
        await cmd.ExecuteNonQueryAsync();
    }

    private static byte[] SerializeVector(float[] vector)
    {
        // Serialize float array to bytes for SQLite storage
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeVector(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
