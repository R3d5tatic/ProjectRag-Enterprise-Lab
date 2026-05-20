namespace ProjectRag.Infrastructure.Options;

internal static class ChunkingStrategyNames
{
    public const string Paragraph = "paragraph";

    public static bool IsSupported(string? strategy)
    {
        return strategy?.Equals(Paragraph, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static string Normalize(string strategy)
    {
        return IsSupported(strategy) ? Paragraph : strategy;
    }
}
