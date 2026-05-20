using ProjectRag.Contracts;
using ProjectRag.Tests.Support;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace ProjectRag.Tests.Evaluation;

public sealed class RagEvaluationTests : IClassFixture<RagApiFactory>
{
    private readonly HttpClient _client;
    private readonly RagApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public RagEvaluationTests(RagApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task Eval_supported_questions_retrieve_expected_source()
    {
        var cases = EvalSetLoader.LoadEvalSet()
            .Where(x => x.ExpectedSourceContains is not null)
            .ToList();

        Assert.NotEmpty(cases);

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-eval-test-");

        try
        {
            SampleDocsTestHelper.CopySampleDocs(tempDirectory.FullName);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(tempDirectory.FullName));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var results = new List<EvalCaseResult>();

            foreach (var evalCase in cases)
            {
                var stopwatch = Stopwatch.StartNew();

                var askResponse = await _client.PostAsJsonAsync(
                    "/api/v1/ask",
                    new AskRequest(evalCase.Question, TopK: 5));

                stopwatch.Stop();

                Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

                var body = await askResponse.Content.ReadFromJsonAsync<AskResponse>();

                Assert.NotNull(body);

                var retrievalHit = body.Citations.Any(citation =>
                    citation.SourceUri.Contains(evalCase.ExpectedSourceContains!, StringComparison.OrdinalIgnoreCase));

                var citationCorrect = body.Claims.Count > 0
                    && body.Claims.All(claim =>
                        claim.CitationChunkIds.Any(chunkId =>
                            body.Citations.Any(citation =>
                                citation.ChunkId == chunkId
                                && citation.SourceUri.Contains(evalCase.ExpectedSourceContains!, StringComparison.OrdinalIgnoreCase))));

                var answerStatusCorrect = body.AnswerStatus == evalCase.ExpectedAnswerStatus;

                results.Add(new EvalCaseResult(
                    Id: evalCase.Id,
                    RetrievalHit: retrievalHit,
                    CitationCorrect: citationCorrect,
                    AnswerStatusCorrect: answerStatusCorrect,
                    Latency: stopwatch.Elapsed));

                Assert.True(retrievalHit, $"Expected source was not retrieved for eval case '{evalCase.Id}'.");
                //Assert.True(citationCorrect, $"Expected source was not cited by all claims for eval case '{evalCase.Id}'.");
                Assert.True(answerStatusCorrect, $"Answer status was incorrect for eval case '{evalCase.Id}'.");
            }

            foreach (var result in results.Where(x => !x.RetrievalHit || !x.AnswerStatusCorrect))
            {
                _output.WriteLine(
                    $"FAILED {result.Id}: retrievalHit={result.RetrievalHit}, statusCorrect={result.AnswerStatusCorrect}");
            }

            var summary = EvalMetrics.Summarize(results);
            _output.WriteLine("Eval summary:");
            _output.WriteLine($"Total cases: {summary.Total}");
            _output.WriteLine($"Retrieval hit rate: {summary.RetrievalHitRate:P0}");
            _output.WriteLine($"Answer status accuracy: {summary.AnswerStatusAccuracy:P0}");
            _output.WriteLine($"Average latency: {summary.AverageLatency.TotalMilliseconds:N0} ms");

            Assert.Equal(cases.Count, summary.Total);
            Assert.Equal(cases.Count, summary.RetrievalHits);
            //Assert.Equal(cases.Count, summary.CorrectCitations);
            Assert.Equal(cases.Count, summary.CorrectAnswerStatuses);
            Assert.True(summary.AverageLatency > TimeSpan.Zero);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }

    [Fact(Skip = "FakeChatClient always returns an answered response; unsupported eval needs configurable answer behavior.")]
    public async Task Eval_unsupported_questions_return_insufficient_context()
    {
        var cases = EvalSetLoader.LoadEvalSet()
            .Where(x => x.ExpectedSourceContains is null)
            .ToList();

        Assert.NotEmpty(cases);

        var tempDirectory = Directory.CreateTempSubdirectory("projectrag-eval-unsupported-test-");

        try
        {
            SampleDocsTestHelper.CopySampleDocs(tempDirectory.FullName);

            var ingestionResponse = await _client.PostAsJsonAsync(
                "/api/v1/ingestions",
                new StartIngestionRequest(tempDirectory.FullName));

            var ingestion = await IngestionApiTestHelper.ReadQueuedIngestionAsync(ingestionResponse);
            await IngestionApiTestHelper.ProcessQueuedIngestionAsync(_factory, ingestion);

            var results = new List<EvalCaseResult>();

            foreach (var evalCase in cases)
            {
                var stopwatch = Stopwatch.StartNew();

                var askResponse = await _client.PostAsJsonAsync(
                    "/api/v1/ask",
                    new AskRequest(evalCase.Question, TopK: 5));

                stopwatch.Stop();

                Assert.Equal(HttpStatusCode.OK, askResponse.StatusCode);

                var body = await askResponse.Content.ReadFromJsonAsync<AskResponse>();

                Assert.NotNull(body);

                var answerStatusCorrect = body.AnswerStatus == evalCase.ExpectedAnswerStatus;
                var citationCorrect = body.Claims.Count == 0;

                results.Add(new EvalCaseResult(
                    Id: evalCase.Id,
                    RetrievalHit: true,
                    CitationCorrect: citationCorrect,
                    AnswerStatusCorrect: answerStatusCorrect,
                    Latency: stopwatch.Elapsed));

                Assert.True(answerStatusCorrect, $"Answer status was incorrect for eval case '{evalCase.Id}'.");
                //Assert.True(citationCorrect, $"Unsupported eval case '{evalCase.Id}' should not return cited claims.");
            }

            var summary = EvalMetrics.Summarize(results);

            Assert.Equal(cases.Count, summary.Total);
            //Assert.Equal(cases.Count, summary.CorrectCitations);
            Assert.Equal(cases.Count, summary.CorrectAnswerStatuses);
            Assert.True(summary.AverageLatency > TimeSpan.Zero);
        }
        finally
        {
            tempDirectory.Delete(recursive: true);
        }
    }
}
