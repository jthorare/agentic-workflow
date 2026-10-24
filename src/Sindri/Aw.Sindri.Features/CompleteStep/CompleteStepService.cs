using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// 'workflow complete' (engine spec rev 8.1 §5.2, §5.3, §6.3): checks each output is at most 1 MiB, claims the nonce for
/// this exact report (SND-WF-009 when it named another command, before anything is stored or sent), checks that the
/// instance's recipe is still approved (SND-RECIPE-005, -006; the claim is given back), stores the outputs
/// owner-only, and sends the 'complete' Update with the nonce as its id. The workflow's validator answers a stale run
/// token (SND-WF-005) or a malformed report (SND-WF-008) synchronously; such a refusal gives the nonce back, so a
/// refused report consumes nothing. A report already applied with this nonce returns the first outcome, marked
/// 'already applied'. Without a nonce one is issued (the stand-in for API P1's IssueNonce).
/// </summary>
/// <param name="directory">Resolves the instance the request names.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="recipes">The approval check (spec §5.2: complete is not exempt).</param>
/// <param name="facts">Where the outputs are stored.</param>
/// <param name="nonces">The nonce table.</param>
/// <param name="callers">The caller the API chain classed: its actor reports.</param>
/// <param name="engineFilled">Which outputs the engine fills, and how it writes them.</param>
internal sealed partial class CompleteStepService(IInstanceDirectory directory, IInstanceRuntime runtime, IApprovedRecipes recipes, IFactStore facts, ICommandNonces nonces, ICallerContextAccessor callers, EngineFilledReport engineFilled)
    : IService<CompleteStepRequest, CompleteStepResponse>
{
    public async ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((Refused(request) ?? engineFilled.Refusal(request.Outputs.Keys)) is { } refusal)   // a hand-written engine-filled output is named from the catalog: no instance read, nothing claimed
        {
            return refusal;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CompleteStep needs a caller");
        }

        var located = await Locate(request, caller, cancellationToken).ConfigureAwait(false);
        if (located.IsError)
        {
            return located.Error;   // a human-only refusal comes before the nonce is claimed: nothing is consumed
        }

        var address = located.Value.Address;
        var read = located.Value.View;
        var nonce = request.Nonce ?? nonces.Issue();
        var reportSha = ReportSha(request, request.Outputs.ToDictionary(static o => o.Key, static o => Convert.ToHexStringLower(SHA256.HashData(o.Value.Span))));   // the caller's outputs: a retry after the engine-filled ones stopped applying hashes the same
        var subject = address.Value + "\n" + reportSha;
        var claim = await nonces.Claim(nonce, subject, cancellationToken).ConfigureAwait(false);
        if (claim.IsError)
        {
            return claim.Error;
        }

        var filled = await Filled(located.Value, request, caller.Actor, claim.Value, cancellationToken).ConfigureAwait(false);
        if (filled.IsError)
        {
            return await GiveBack(nonce, claim.Value, filled.Error, cancellationToken).ConfigureAwait(false);
        }

        var outputs = engineFilled.Merge(request.Outputs, filled.Value);
        return await Report(address, new SentReport(nonce, subject, claim.Value), new StepReport(request, caller.Actor, reportSha, outputs, located.Value.Via), read, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the instance and applies the human-only refusal (DT spec §8.5): a session report reads the instance and asks the catalog
    /// about its open task; a session report on an instance that can't be read is refused with that read error (every session report: plan Q7, DT spec §8.5), not let
    /// through. A session report passes the view it read on to the engine-filled outputs and the approval check, so it reads the instance once. A human's report isn't read here: a fresh one reads on demand when it fills the engine's outputs.
    /// </summary>
    private async ValueTask<Result<Located>> Locate(CompleteStepRequest request, CallerContext caller, CancellationToken cancellationToken)
    {
        var resolved = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        if (caller.Class == CallerClass.Human)
        {
            return new Located(resolved.Value, View: null, StepRuntime.Desk);
        }

        var view = await runtime.Get(resolved.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var via = engineFilled.Authorize(view.Value, request);
        if (via.IsError)
        {
            return via.Error;
        }

        if (via.Value == StepRuntime.Agent && await engineFilled.OutputTypeRefusal(view.Value, request, cancellationToken).ConfigureAwait(false) is { } untyped)
        {
            return untyped;   // before the nonce is claimed and anything is stored: a refused report consumes nothing
        }

        return new Located(resolved.Value, view.Value, via.Value);
    }

    /// <summary>Checks approval (a fresh claim only, as ApplyCommand does), stores the outputs and sends the report.</summary>
    private async ValueTask<Result<CompleteStepResponse>> Report(InstanceAddress address, SentReport sentReport, StepReport step, InstanceView? read, CancellationToken cancellationToken)
    {
        var stored = sentReport.Claim == NonceClaim.Fresh && await Approved(address, read, cancellationToken).ConfigureAwait(false) is { IsError: true } refused
            ? refused.Error
            : await Store(step.Outputs, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return await GiveBack(sentReport.Nonce, sentReport.Claim, stored.Error, cancellationToken).ConfigureAwait(false);
        }

        var report = new StepCompleted(step.Actor, step.Request.Run, step.Request.Outcome, stored.Value, Tokens: 0, step.Via, step.Sha, FindingKeys: FindingKeysOf(step.Outputs));
        return await Send(address, sentReport, report, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>What a report says: the request, the actor reporting, the digest of the caller's outputs and the outputs to store (the caller's plus the engine-filled ones) and the runtime that reported (agent for a proven launched session, else desk).</summary>
    private readonly record struct StepReport(CompleteStepRequest Request, string Actor, string Sha, IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> Outputs, StepRuntime Via);

    private static Error? Refused(CompleteStepRequest request) =>
        request.Outputs.Where(static o => o.Value.Length > IFactStore.MaxBytes).Select(static o => o.Key.Value).FirstOrDefault() is { } large
            ? SindriErrors.InvalidInput.With($"{large} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)")
            : null;

    /// <summary>A report is not exempt from the approval check (spec §5.2): the instance's recipe must still hold, else nothing is stored or sent.</summary>
    private async ValueTask<Result<Unit>> Approved(InstanceAddress address, InstanceView? read, CancellationToken cancellationToken)
    {
        var view = read is null ? await runtime.Get(address, cancellationToken).ConfigureAwait(false) : Results.Success(read);
        if (view.IsError)
        {
            return view.Error;
        }

        var approval = await recipes.Check(view.Value.Recipe, cancellationToken).ConfigureAwait(false);
        return approval.IsError ? approval.Error : ApprovalRefusal.Of(approval.Value, view.Value.Recipe.Id, view.Value.Recipe.Hash, address);
    }

    /// <summary>Sends the report as the Update keyed by the nonce; a refusal gives back a claim this call created, an unconfirmed send keeps it.</summary>
    private async ValueTask<Result<CompleteStepResponse>> Send(InstanceAddress address, SentReport sentReport, StepCompleted report, CancellationToken cancellationToken)
    {
        var sent = await runtime.Send(address, sentReport.Nonce, report, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            return sent.Error.Kind is ErrorKind.Unavailable or ErrorKind.Timeout
                ? sent.Error   // it may have applied: keep the claim, so the retry with this nonce resends the same Update id
                : await GiveBack(sentReport.Nonce, sentReport.Claim, sent.Error, cancellationToken).ConfigureAwait(false);
        }

        var applied = await nonces.Applied(sentReport.Nonce, sentReport.Subject, cancellationToken).ConfigureAwait(false);
        return applied.IsError ? applied.Error : new CompleteStepResponse(sent.Value, sentReport.Nonce, sentReport.Claim == NonceClaim.Applied);
    }

    /// <summary>A refused report consumes nothing (spec §5.2, W12): a claim this call created is given back; a pending or applied one stays, as its Update id was used.</summary>
    private async ValueTask<Result<CompleteStepResponse>> GiveBack(CommandNonce nonce, NonceClaim claim, Error refusal, CancellationToken cancellationToken)
    {
        if (claim == NonceClaim.Fresh)
        {
            _ = await nonces.Release(nonce, cancellationToken).ConfigureAwait(false);
        }

        return refusal;
    }

    /// <summary>The instance a request names and, for a session report, the view read to apply the human-only rule (null for a human, whose report reads on demand).</summary>
    private readonly record struct Located(InstanceAddress Address, InstanceView? View, StepRuntime Via);

    /// <summary>The nonce a report went out with, the subject it claimed it for and what the claim found.</summary>
    private readonly record struct SentReport(CommandNonce Nonce, string Subject, NonceClaim Claim);

    /// <summary>The engine-filled outputs, from the view a session report already read (a human's reads on demand); a retry (a pending or applied claim) needs no view and sends without them when the instance can't be read: Temporal answers its Update id with the first result.</summary>
    private async ValueTask<Result<IReadOnlyDictionary<FactKey, DecisionFact>>> Filled(Located located, CompleteStepRequest request, string actor, NonceClaim claim, CancellationToken cancellationToken)
    {
        var filled = await EngineFilled(located, request, actor, cancellationToken).ConfigureAwait(false);
        return filled.IsError && claim != NonceClaim.Fresh
            ? Results.Success<IReadOnlyDictionary<FactKey, DecisionFact>>(new Dictionary<FactKey, DecisionFact>())
            : filled;
    }

    /// <summary>
    /// The outputs the engine writes for this report (DT spec §8.1: 'fact:decision' of the choice Steps, from --outcome and the
    /// caller's actor, provider "desk"); none when the instance's open task isn't the one reported on (a retry after it moved
    /// on, or a stale run: the send answers). Reads the instance, so a fresh report on an instance that can't be read is that
    /// error; a retry sends without (see <see cref="Handle"/>). A hand-written engine-filled output was refused before this.
    /// </summary>
    private async ValueTask<Result<IReadOnlyDictionary<FactKey, DecisionFact>>> EngineFilled(Located located, CompleteStepRequest request, string actor, CancellationToken cancellationToken)
    {
        var view = located.View is null ? await runtime.Get(located.Address, cancellationToken).ConfigureAwait(false) : Results.Success(located.View);
        if (view.IsError)
        {
            return view.Error;
        }

        return view.Value.Task is not { } task || task.Run != request.Run
            ? Results.Success<IReadOnlyDictionary<FactKey, DecisionFact>>(new Dictionary<FactKey, DecisionFact>())
            : Results.Success(EngineFilledOutputs.For(task.Outputs, request.Outcome, "desk", actor));
    }

    private async ValueTask<Result<IReadOnlyDictionary<FactKey, FactValue>>> Store(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, CancellationToken cancellationToken)
    {
        var stored = new Dictionary<FactKey, FactValue>();
        foreach (var (key, content) in outputs)
        {
            var put = await facts.Put(content, cancellationToken).ConfigureAwait(false);
            if (put.IsError)
            {
                return put.Error;
            }

            stored[key] = put.Value;
        }

        return stored;
    }

    /// <summary>The SHA-256 of the canonical report (run, outcome, each output's key and content digest in key order), so two different reports never share a key.</summary>
    internal static string ReportSha(CompleteStepRequest request, Dictionary<FactKey, string> shas)
    {
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"run {request.Run.Value}\noutcome {request.Outcome.Value}\n");
        foreach (var (key, sha) in shas.OrderBy(static s => s.Key.Value, StringComparer.Ordinal))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"output {key.Value} {sha}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
