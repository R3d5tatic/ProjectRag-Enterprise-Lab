namespace ProjectRag.Ingestion.Worker.options;

internal sealed class WorkerOptions
{
    public const string SectionName = "Worker";
    public int PollIntervalSeconds { get; set; } = 5;
}
