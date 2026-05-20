namespace ProjectRag.Infrastructure.AI;

internal static class JsonObjectExtractor
{
    public static string Extract(string responseText)
    {
        var start = responseText.IndexOf('{');
        var end = responseText.LastIndexOf('}');

        return start >= 0 && end > start
            ? responseText[start..(end + 1)]
            : responseText;
    }
}
