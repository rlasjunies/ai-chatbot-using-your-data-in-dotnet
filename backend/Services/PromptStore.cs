using Microsoft.Data.Sqlite;

namespace ChatBot.Services;

/// <summary>
/// Manages prompts in SQLite database for single-file deployment.
/// Provides immediate effect - no caching, always reads from DB.
/// </summary>
public class PromptStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private const string DatabasePath = "promptstore.db";

    public PromptStore()
    {
        _connection = new SqliteConnection($"Data Source={DatabasePath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS prompts (
                name TEXT PRIMARY KEY,
                content TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Gets a prompt by name. Returns null if not found.
    /// </summary>
    public string? GetPrompt(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT content FROM prompts WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return reader.GetString(0);
        }

        return null;
    }

    /// <summary>
    /// Sets or updates a prompt. Creates if doesn't exist, updates if exists.
    /// </summary>
    public void SetPrompt(string name, string content)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prompts (name, content, updated_at)
            VALUES ($name, $content, $updated_at)
            ON CONFLICT(name) DO UPDATE SET
                content = $content,
                updated_at = $updated_at
            """;
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$content", content);
        cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Lists all prompts with their metadata.
    /// </summary>
    public List<(string Name, string Content, DateTime UpdatedAt)> ListAll()
    {
        var prompts = new List<(string, string, DateTime)>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name, content, updated_at FROM prompts ORDER BY name";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(0);
            var content = reader.GetString(1);
            var updatedAt = DateTime.Parse(reader.GetString(2));
            prompts.Add((name, content, updatedAt));
        }

        return prompts;
    }

    /// <summary>
    /// Resets all prompts to default values from EmbeddedPrompts.
    /// </summary>
    public void ResetToDefaults()
    {
        SetPrompt("ChatSystemPrompt", EmbeddedPrompts.ChatSystemPrompt);
        SetPrompt("HydePrompt", EmbeddedPrompts.HydePrompt);
        SetPrompt("RagSystemPrompt", EmbeddedPrompts.RagSystemPrompt);
    }

    /// <summary>
    /// Checks if the prompts table is empty (for initial seeding).
    /// </summary>
    public bool IsEmpty()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM prompts";
        var count = (long)cmd.ExecuteScalar()!;
        return count == 0;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
