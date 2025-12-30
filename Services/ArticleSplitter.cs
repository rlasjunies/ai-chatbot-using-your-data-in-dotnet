using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Text;
using ChatBot.Models;

namespace ChatBot.Services;

public class ArticleSplitter(int MaxTokensPerChunk = 300, int OverlapTokens = 60)
{

    /// <summary>
    /// Extremely basic token estimator (~4 chars ≈ 1 token).
    /// Good enough for window sizing in a course project.
    /// </summary>
    public static int EstimateTokens(string text) => (int)Math.Ceiling(text.Length / 4.0);

    /// Make "lines" for TextChunker. It works best when lines aren’t giant.
    /// We first split by newlines; if a line is very long, we softly wrap it.
    public static List<string> SplitLines(string text, int softWrapChars = 400)
    {
        var raw = text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => s.Trim())
                      .Where(s => s.Length > 0);

        List<string> lines = new();
        foreach (var line in raw)
        {
            if (line.Length <= softWrapChars)
            {
                lines.Add(line);
                continue;
            }

            // Soft wrap long lines to ~softWrapChars chunks on word boundaries
            int index = 0;
            while (index < line.Length)
            {
                int remaining = line.Length - index;
                int take = Math.Min(softWrapChars, remaining);

                // Try not to break in the middle of a word
                int end = index + take;
                if (end < line.Length)
                {
                    int lastSpace = line.LastIndexOf(' ', end - 1, take);
                    if (lastSpace > index + softWrapChars / 2) end = lastSpace;
                }

                lines.Add(line.Substring(index, end - index).Trim());
                index = end;
                while (index < line.Length && line[index] == ' ') index++; // skip spaces
            }
        }

        return lines;
    }

    public IEnumerable<DocumentChunk> Chunk(string title, string content, string pageUrl = "", string section = "")
    {
        // Prepare lines for TextChunker (short-ish lines work best).
        var lines = SplitLines(content);

        // Split into overlapping paragraphs (chunks).
        var chunkBodies = TextChunker.SplitPlainTextParagraphs(
            lines: lines,
            maxTokensPerParagraph: MaxTokensPerChunk,
            overlapTokens: OverlapTokens,
            chunkHeader: null,
            tokenCounter: EstimateTokens
        );

        return chunkBodies.Select((chunkContent, index) => new DocumentChunk(
            Id: Utils.ToUrlSafeId($"{title}_{section}_{index + 1:D2}"),
            Title: title,
            Section: section,
            ChunkIndex: index + 1,
            Content: chunkContent.Trim(),
            SourcePageUrl: $"{pageUrl}#{Utils.ToUrlSafeId(section)}"
        ));

    }
}