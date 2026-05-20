namespace ProjectRag.Infrastructure.Options;

internal sealed class ChunkingOptions
{
    public const string SectionName = "Chunking";

    public string Strategy { get; set; } = ChunkingStrategyNames.Paragraph;
    public int MaxChunkSize { get; set; } = 1200;
}
