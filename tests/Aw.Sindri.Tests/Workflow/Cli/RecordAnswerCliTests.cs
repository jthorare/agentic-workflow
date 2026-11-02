using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri workflow answer &lt;address&gt; --record &lt;batch.json&gt;' (#1270), through the verb table exactly as Program runs it, against the real host behind a
/// stand-in for the routes it reads and calls: the instance list, the interview instance's detail (the scoping-interview fixture, whose desk run is 6.1), the
/// answer log (A1 only) and the 'complete' route, which records what it is sent. A batch that supersedes an answer the log does not hold is refused before
/// anything is sent, naming it; a good one is sent as the desk's own complete call: run 6.1, outcome 'answered', the file as 'fact:answers'.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class RecordAnswerCliTests : IDisposable
{
    private const string Good = "[{\"batch\":2,\"round\":2,\"actor\":\"owner\",\"recordedAs\":[\"AC-1\"],\"supersedes\":[\"A1\"],\"answers\":[{\"question\":\"Q1\",\"option\":\"Q1.b\",\"text\":\"Never expire\"}]}]";
    private const string Typo = "[{\"batch\":2,\"round\":2,\"actor\":\"owner\",\"recordedAs\":[\"AC-1\"],\"supersedes\":[\"A9\"],\"answers\":[{\"question\":\"Q1\",\"option\":\"Q1.b\",\"text\":\"Never expire\"}]}]";

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public RecordAnswerCliTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_batch_that_supersedes_an_answer_the_log_does_not_hold_is_refused_naming_it_and_sends_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var completes = new List<(string Path, string Body)>();
        await using var app = await Started(completes, ct);

        var exit = await Run(Typo, ct);

        Assert.NotEqual(0, exit);
        Assert.Contains("A9", _out.ToString() + _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(completes);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_good_batch_is_sent_as_the_desks_complete_call_for_the_interview_run()
    {
        var ct = TestContext.Current.CancellationToken;
        var completes = new List<(string Path, string Body)>();
        await using var app = await Started(completes, ct);

        _ = await Run(Good, ct);

        var (path, body) = Assert.Single(completes);
        Assert.EndsWith("/runs/6.1/complete", path, StringComparison.Ordinal);
        Assert.Contains("answered", body, StringComparison.Ordinal);
        Assert.Contains("fact:answers", body, StringComparison.Ordinal);
    }

    private async Task<int> Run(string batch, CancellationToken ct)
    {
        var file = Path.Combine(_home.Path, "batch.json");
        await File.WriteAllTextAsync(file, batch, ct);
        var address = WorkflowViewFixtures.AllSummaries()[0].Address.Value;
        return await CommandTable.Run(["workflow", "answer", address, "--record", file, "--nonce", new string('a', 32)], name => name == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System);
    }

    private Task<WebApplication> Started(List<(string Path, string Body)> completes, CancellationToken ct)
    {
        var list = new InstanceList([WorkflowViewFixtures.AllSummaries()[0]], new ListCounts(1, 0, 0, 0, 0), Empty: null, WorkflowViewFixtures.Now);
        var log = new FactView("fact:answerLog", FactType.Payload, JsonDocument.Parse("[{\"id\":\"A1\"}]").RootElement.Clone(), Truncated: false);
        return CliHost.Started(_home, ct, app => app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value!;
            if (context.Request.Method == "POST" && path.EndsWith("/complete", StringComparison.Ordinal))
            {
                using var reader = new StreamReader(context.Request.Body);
                completes.Add((path, await reader.ReadToEndAsync(ct)));
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
            else if (context.Request.Method == "GET" && path.Contains("/facts/", StringComparison.Ordinal))
            {
                await Json(context, AwJson.Serialize(log, SliceHarness.Json), ct);
            }
            else if (context.Request.Method == "GET" && path.EndsWith("/workflow/instances", StringComparison.Ordinal))
            {
                await Json(context, AwJson.Serialize(list, SliceHarness.Json), ct);
            }
            else if (context.Request.Method == "GET" && path.Contains("/workflow/instances/", StringComparison.Ordinal))
            {
                await Json(context, AwJson.Serialize(WorkflowViewFixtures.ScopingInterview(), SliceHarness.Json), ct);
            }
            else
            {
                await next(context);
            }
        }));
    }

    private static async Task Json(HttpContext context, byte[] body, CancellationToken ct)
    {
        context.Response.ContentType = "application/json";
        await context.Response.Body.WriteAsync(body, ct);
    }
}
