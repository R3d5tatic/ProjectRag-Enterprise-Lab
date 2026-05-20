using ProjectRag.Infrastructure;
using ProjectRag.Ingestion.Worker;
using ProjectRag.Ingestion.Worker.options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureForWorker(builder.Configuration);
builder.Services.AddHostedService<Worker>();

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .Validate(x => x.PollIntervalSeconds > 0, "Worker poll interval must be greater than zero.")
    .ValidateOnStart();

var host = builder.Build();
host.Run();