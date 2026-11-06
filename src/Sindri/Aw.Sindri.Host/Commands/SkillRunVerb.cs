using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.StartSkillRun;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri skill run &lt;id&gt; …' (skill flows spec §7): runs one skill block as a trial. It makes an item on the repo, starts the block's trial run on it through the running control plane (it never starts it) and, with
/// '--follow', waits for the run to end and prints how. Without '--follow': exit 0 started, 1 not running (SND-API-001), 2 refused or invalid (an unknown block, a missing required input, a bad '--input'). With it: 0 when the block ended on its
/// success outcome, 3 on another mapped outcome, 4 on step.failed. A trial writes to the owner's real plans folder, which the header says.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verb writes.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="clock">Paces the follow loop.</param>
internal sealed class SkillRunVerb(ControlPlaneClient client, Output output, Func<string, string?> environment, TimeProvider clock)
{
    private static readonly TimeSpan _poll = TimeSpan.FromSeconds(2);

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var parsed = await SkillRunArguments.Parse(args, currentDirectory, ct).ConfigureAwait(false);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var run = parsed.Value;
        var started = await Start(run, ct).ConfigureAwait(false);
        if (started.IsError)
        {
            return await output.Error(started.Error).ConfigureAwait(false);
        }

        var (answer, body) = (started.Value.Value, started.Value.Body);
        var awDir = SkillAwDir.Of(environment, currentDirectory);
        if (!run.Follow)
        {
            return run.Json
                ? await output.Raw(body, 0).ConfigureAwait(false)
                : await output.Text($"{answer.Skill}  manifest sha256:{answer.ManifestHash}\nWrites to your real plans under {awDir}\nstarted {answer.Instance.Address.Value}\n", 0).ConfigureAwait(false);
        }

        if (!run.Json)
        {
            await output.Notice($"Writes to your real plans under {awDir}").ConfigureAwait(false);
        }

        var report = await Follow(answer, ct).ConfigureAwait(false);
        if (report.IsError)
        {
            return await output.Error(report.Error).ConfigureAwait(false);
        }

        var code = SkillRunText.ExitCode(report.Value);
        return run.Json ? await output.Json(report.Value, code).ConfigureAwait(false) : await output.Text(SkillRunText.Render(report.Value), code).ConfigureAwait(false);
    }

    /// <summary>Makes the item the trial runs on, then starts the block on it.</summary>
    private async Task<Result<ApiAnswer<StartSkillRunResponse>>> Start(SkillRunArguments run, CancellationToken ct)
    {
        RepoName? repo = null;
        if (run.Repo is { } text)
        {
            if (!RepoName.TryFrom(text, out var typed))
            {
                return SindriErrors.InvalidInput.With($"'{text}' isn't a repo name");
            }

            repo = typed;
        }

        var item = await client.Post<CreateItemRequest, ItemCreatedResponse>("/sindri/items", new CreateItemRequest($"Try the {run.Block} block", repo), ct).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var id = item.Value.Value.ItemId.Value;
        var started = await client.Post<StartSkillRunBody, StartSkillRunResponse>($"/sindri/skills/{Uri.EscapeDataString(run.Block)}/runs", new StartSkillRunBody(id, run.Inputs, run.Skills), ct).ConfigureAwait(false);
        if (started.IsError)
        {
            await CancelItem(id, ct).ConfigureAwait(false);   // the refusal is the answer; the item it was made for must not stay open
        }

        return started;
    }

    /// <summary>Closes the trial's item as cancelled, best effort: a failure here must not replace the refusal the caller is about to report.</summary>
    private async Task CancelItem(string id, CancellationToken ct)
    {
        var route = "/sindri/items/" + Uri.EscapeDataString(id);
        var read = await client.GetView<ItemView>(route, ct).ConfigureAwait(false);
        if (read.IsSuccess)
        {
            _ = await client.Post<CloseItemBody, CommandAccepted>(route + "/close", new CloseItemBody(CloseReason.Cancelled, read.Value.Value.Version), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Polls the instance until it sits in a terminal state, then reads its outcome, the facts the block wrote and, for a failed Step, why.</summary>
    private async Task<Result<SkillRunReport>> Follow(StartSkillRunResponse answer, CancellationToken ct)
    {
        var route = $"{InstanceReads.Instances}/{InstanceRoute.Segment(answer.Instance.Id.Value)}";
        while (true)
        {
            var got = await client.Get<InstanceDetail>(route, ct).ConfigureAwait(false);
            if (got.IsError)
            {
                return InstanceReads.NotRunning(got.Error);
            }

            var detail = got.Value.Value;
            if (detail.States.FirstOrDefault(s => string.Equals(s.State, detail.State, StringComparison.Ordinal) && s.Terminal) is { } ended)
            {
                return await Report(answer, detail, ended, route, ct).ConfigureAwait(false);
            }

            await Task.Delay(_poll, clock, ct).ConfigureAwait(false);
        }
    }

    private async Task<Result<SkillRunReport>> Report(StartSkillRunResponse answer, InstanceDetail detail, StateCell ended, string route, CancellationToken ct)
    {
        var failed = string.Equals(detail.State, "failed", StringComparison.Ordinal);
        var facts = failed ? [] : detail.Facts
            .Where(f => f.Direction == FactDirection.Output && f.Status == FactStatus.Produced && answer.Outputs.Contains(f.Key, StringComparer.Ordinal))
            .Select(static f => new SkillRunFact(f.Key, f.Summary)).ToList();
        string? error = null;
        if (failed)
        {
            var events = await client.Get<GetInstanceEventsResponse>(route + "/events", ct).ConfigureAwait(false);
            if (events.IsError)
            {
                return InstanceReads.NotRunning(events.Error);
            }

            error = events.Value.Value.Events.LastOrDefault(static e => e.Transition.Reason is not null)?.Transition.Reason;
        }

        var outcome = failed ? null : ended.Outcome;
        var resultFile = await ResultFileOf(answer.Instance.Address.Value.Split('/')[0], ct).ConfigureAwait(false);
        return new SkillRunReport(answer.Skill, answer.ManifestHash, outcome, Succeeded: string.Equals(outcome, answer.Success, StringComparison.Ordinal), facts, resultFile, error);
    }

    /// <summary>The result file of the item's latest skill launch: <c lang="text">skill.resultFile</c> of its newest 'launch.started', read from the ledger; empty when the ledger has none or can't be read (the report then leaves the line out).</summary>
    /// <param name="item">The item the trial ran on.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The path, or empty.</returns>
    internal async Task<string> ResultFileOf(string item, CancellationToken ct)
    {
        var page = await client.Get<LedgerPage>($"/sindri/ledger?item={Uri.EscapeDataString(item)}&type=launch.started", ct).ConfigureAwait(false);
        if (page.IsError)
        {
            return string.Empty;
        }

        foreach (var started in page.Value.Value.Events.Reverse())
        {
            if (started.Payload.ValueKind == JsonValueKind.Object && started.Payload.TryGetProperty("skill", out var skill) && skill.ValueKind == JsonValueKind.Object
                && skill.TryGetProperty("resultFile", out var file) && file.ValueKind == JsonValueKind.String && file.GetString() is { Length: > 0 } path)
            {
                return path;
            }
        }

        return string.Empty;
    }
}
