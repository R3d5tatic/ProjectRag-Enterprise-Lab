using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Infrastructure.Ingestion;

internal sealed class SimpleTextChunker : ITextChunker
{
    private readonly ChunkingOptions _options;

    public SimpleTextChunker(IOptions<ChunkingOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var paragraphs = text
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var chunks = new List<TextChunk>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            if (currentLength + paragraph.Length > _options.MaxChunkSize && current.Count > 0)
            {
                AddChunk(chunks, current);
                current.Clear();
                currentLength = 0;
            }

            current.Add(paragraph);
            currentLength += paragraph.Length;
        }

        if (current.Count > 0)
        {
            AddChunk(chunks, current);
        }

        return chunks;
    }

    private static void AddChunk(List<TextChunk> chunks, List<string> paragraphs)
    {
        var text = string.Join("\n\n", paragraphs).Trim();

        chunks.Add(new TextChunk(
            ChunkIndex: chunks.Count,
            Text: text,
            SectionTitle: ExtractSectionTitle(text)));
    }

    private static string? ExtractSectionTitle(string text)
    {
        var firstLine = text
            .Split("\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return firstLine is not null && firstLine.StartsWith("# ", StringComparison.Ordinal)
            ? firstLine[2..].Trim()
            : null;
    }
}
