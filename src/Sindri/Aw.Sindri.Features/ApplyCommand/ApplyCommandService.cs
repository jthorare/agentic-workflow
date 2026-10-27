using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// 'workflow answer', 'command', 'cancel', 'takeover' and 'handback' (engine spec rev 8 §5.2, §5.8, §6.2, §6.3). In order: resolve the address
/// (a bare item is the root of its latest attempt, SND-WF-006 when ambiguous); check the arguments; claim the nonce for
/// exactly this command (SND-WF-009 when it named another); redeem a protected answer's challenge (SND-WF-013); check
/// the recipe's approval unless the trigger is exempt (cancel, takeover, handback; SND-RECIPE-005, -006); send the Update with the nonce
/// as its id. A repeat of a command its nonce already holds (applied, or pending after an unconfirmed send) skips the checks and resends the same Update id: Temporal returns the first outcome. The workflow's validator answers a trigger not allowed here or an answer that doesn't fit (SND-WF-003);
/// a refusal gives back a nonce this call claimed, so a refused command consumes nothing. A command already applied with this nonce
/// returns the first outcome, marked 'already applied'. Without a nonce one is issued (the IssueNonce stand-in).
/// The caller and action-class checks (SND-AUTH-002) are the API chain's, before this slice; the caller's
/// actor sends the command.
/// </summary>
/// <param name="directory">Resolves the address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="recipes">The approval check.</param>
/// <param name="nonces">The nonce table.</param>
/// <param name="callers">The caller the API chain classed: its actor sends the command.</param>
/// <param name="sessions">The agent sessions the ledger shows: a rewind passes the launch of an ended session to the engine, which refuses a run whose agent is live; null reads none.</param>
internal sealed class ApplyCommandService(IInstanceDirectory directory, IInstanceRuntime runtime, IApprovedRecipes recipes, ICommandNonces nonces,
    ICallerContextAccessor callers, IAgentSessions? sessions = null)
    : IService<ApplyCommandRequest, ApplyCommandResponse>
{
    public async ValueTask<Result<ApplyCommandResponse>> Handle(ApplyCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Malformed(request) is { } malformed)
        {
            return malformed;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("ApplyCommand needs a caller");
        }

        var address = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var nonce = request.Nonce ?? nonces.Issue();
        var stamp = new CommandStamp(nonce, ArgumentsSha(request));
        var trigger = await Ended(Trigger(request, caller.Actor, stamp), address.Value, cancellationToken).ConfigureAwait(false);
        var subject = address.Value.Value + "\n" + stamp.ArgumentsSha256;
        var claim = await nonces.Claim(nonce, subject, cancellationToken).ConfigureAwait(false);
        if (claim.IsError)
        {
            return claim.Error;
        }

        return await Deliver(request, address.Value, new SentCommand(nonce, subject, claim.Value), (trigger, caller), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Routes the command (spec §5.8), checks it when this call created the claim, and sends it.</summary>
    // A rewind refuses a run whose agent is live. The engine can't read the ledger, so the launch whose session already ended (failed, idle, lost) is passed in.
    private async ValueTask<Trigger> Ended(Trigger trigger, InstanceAddress address, CancellationToken cancellationToken)
    {
        if (trigger is not RewindRequested rewind || sessions is null)
        {
            return trigger;
        }

        var view = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
        if (view.IsError || view.Value.AgentRun?.Launch is not { Length: > 0 } launch || !LaunchId.TryFrom(launch, out var id))
        {
            return trigger;
        }

        var state = await sessions.Watch(id, cancellationToken).ConfigureAwait(false);
        return !state.IsError && !state.Value.IsLive ? rewind with { EndedLaunch = launch } : trigger;
    }

    private async ValueTask<Result<ApplyCommandResponse>> Deliver(ApplyCommandRequest request, InstanceAddress address, SentCommand command, (Trigger Trigger, CallerContext Caller) call, CancellationToken cancellationToken)
    {
        var route = await Route(request, address, new Dispatch(command.Nonce, call.Trigger), cancellationToken).ConfigureAwait(false);
        if (route.IsError)
        {
            return await Refused(route.Error, command.Nonce, command.Claim, cancellationToken).ConfigureAwait(false);
        }

        if (await LaunchedRefusal(request, call.Caller, route.Value, cancellationToken).ConfigureAwait(false) is { } launched)
        {
            return await Refused(launched, command.Nonce, command.Claim, cancellationToken).ConfigureAwait(false);
        }

        // only a claim this call created is checked (a pending or applied one was checked by the call that made it: a protected
        // answer's challenge is single use and bound to the version, so a second check would refuse an answer that applied)
        if (command.Claim == NonceClaim.Fresh && await Check(request, route.Value, cancellationToken).ConfigureAwait(false) is { IsError: true } refused)
        {
            return await Refused(refused.Error, command.Nonce, command.Claim, cancellationToken).ConfigureAwait(false);
        }

        return await Send(route.Value.Target, command, route.Value.Dispatch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A launched session's answer or command on a human step (q100, <see cref="LaunchedCallers"/>); null for any other call.</summary>
    private async ValueTask<Error?> LaunchedRefusal(ApplyCommandRequest request, CallerContext caller, Routing route, CancellationToken cancellationToken)
    {
        if (caller.Launch is not null && request.Kind == ApplyCommandKind.Rewind)
        {
            return SindriErrors.AuthRefused.With("workflow rewind is a human step: a session Sindri launched can't rewind an instance. Ask the human, or an agent session the owner started");
        }

        if (caller.Launch is null || request.Kind is not (ApplyCommandKind.Answer or ApplyCommandKind.Command))
        {
            return null;
        }

        var view = await ViewOf(route, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var action = request.Kind == ApplyCommandKind.Answer ? "workflow answer" : "workflow command " + request.Verb?.Value;
        return LaunchedCallers.RefuseOnHumanStep(caller, view.Value, action);
    }

    /// <summary>A refusal before the Update gives back a claim this call created, so a refused command consumes nothing.</summary>
    private async ValueTask<Result<ApplyCommandResponse>> Refused(Error error, CommandNonce nonce, NonceClaim claim, CancellationToken cancellationToken)
    {
        if (claim == NonceClaim.Fresh)
        {
            _ = await nonces.Release(nonce, cancellationToken).ConfigureAwait(false);
        }

        return error;
    }

    /// <summary>
    /// Where the Update goes (spec §5.8, W17): an answer to a parent whose open park or escalation mirrors a child goes to the
    /// child, as the child's own Update under a nonce minted for the child (derived from the parent's, so a retry resends the
    /// same Update id) and with 'onBehalfOf' naming the parent and the actor. Every other command acts on the instance given.
    /// </summary>
    private async ValueTask<Result<Routing>> Route(ApplyCommandRequest request, InstanceAddress address, Dispatch dispatch, CancellationToken cancellationToken)
    {
        if (request.Kind != ApplyCommandKind.Answer)
        {
            return new Routing(address, dispatch, Parent: null);
        }

        var parent = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
        if (parent.IsError)
        {
            return parent.Error;
        }

        if (parent.Value.ChildPark is not { } child || dispatch.Trigger is not AnswerGiven answer)
        {
            return new Routing(address, dispatch, parent.Value);
        }

        var wire = ChildNonce(dispatch.Nonce, child.Address);
        var forwarded = answer with { Stamp = answer.Stamp with { Nonce = wire }, Forwarded = new OnBehalfOf(parent.Value.Id, answer.Actor) };
        return new Routing(child.Address, new Dispatch(wire, forwarded), Parent: null);
    }

    /// <summary>The nonce a child's Update carries: a digest of the parent's nonce and the child, so one parent command is one child Update.</summary>
    private static CommandNonce ChildNonce(CommandNonce parent, InstanceAddress child) =>
        CommandNonce.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("forward\n" + parent.Value + "\n" + child.Value)))[..32]);

    /// <summary>The Update to send, with the id it goes under.</summary>
    private readonly record struct Dispatch(CommandNonce Nonce, Trigger Trigger);

    /// <summary>The instance the Update goes to, what is sent, and the parent's view when the routing already read it.</summary>
    private readonly record struct Routing(InstanceAddress Target, Dispatch Dispatch, InstanceView? Parent);

    /// <summary>
    /// Sends the Update with the nonce as its id. A refusal of a claim this call created gives it back, so a refused command
    /// consumes nothing (W12); a pending claim stays (its Update id was used, and Temporal returns the first outcome for it).
    /// </summary>
    private async ValueTask<Result<ApplyCommandResponse>> Send(InstanceAddress address, SentCommand command, Dispatch dispatch, CancellationToken cancellationToken)
    {
        var sent = await runtime.Send(address, dispatch.Nonce, dispatch.Trigger, cancellationToken).ConfigureAwait(false);
        if (sent.IsError)
        {
            if (sent.Error.Kind is not (ErrorKind.Unavailable or ErrorKind.Timeout) && command.Claim == NonceClaim.Fresh)
            {
                _ = await nonces.Release(command.Nonce, cancellationToken).ConfigureAwait(false);
            }

            return sent.Error;   // unavailable or timed out: it may have applied, so the claim stays and a retry with this nonce resends the same Update id
        }

        var applied = await nonces.Applied(command.Nonce, command.Subject, cancellationToken).ConfigureAwait(false);
        return applied.IsError ? applied.Error : new ApplyCommandResponse(sent.Value, command.Nonce, command.Claim == NonceClaim.Applied);
    }

    /// <summary>The nonce a command went out with, the subject it claimed it for and what the claim found.</summary>
    private readonly record struct SentCommand(CommandNonce Nonce, string Subject, NonceClaim Claim);

    /// <summary>
    /// The checks before the Update (spec §4.2, §5.2): a protected answer (a grant or an accept) asks for no typed challenge (owner decision q93) but is checked like any
    /// other; a human trigger that isn't exempt needs its recipe's approval to hold.
    /// </summary>
    private async ValueTask<Result<Unit>> Check(ApplyCommandRequest request, Routing route, CancellationToken cancellationToken)
    {
        var protectedAnswer = ApplyCommandChallenge.IsProtected(request.Kind, request.Answer);
        if (!protectedAnswer && route.Dispatch.Trigger.ExemptFromApproval)
        {
            return Unit.Value;
        }

        var address = route.Target;   // a forwarded answer is checked against the child: its challenge is bound to the child's address and version
        var view = await ViewOf(route, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var approval = await recipes.Check(view.Value.Recipe, cancellationToken).ConfigureAwait(false);
        if (approval.IsError)
        {
            return approval.Error;
        }

        return ApprovalRefusal.Of(approval.Value, view.Value.Recipe.Id, view.Value.Recipe.Hash, address);
    }

    /// <summary>The view the check reads: the parent's, when routing already read it, else the target's.</summary>
    private ValueTask<Result<InstanceView>> ViewOf(Routing route, CancellationToken cancellationToken) =>
        route.Parent is { } known ? ValueTask.FromResult<Result<InstanceView>>(known) : runtime.Get(route.Target, cancellationToken);

    /// <summary>The arguments each kind needs (spec §6.3): an answer's variant and its text or amount, a command's verb.</summary>
    private static Error? Malformed(ApplyCommandRequest request) => request switch
    {
        { Kind: ApplyCommandKind.Answer, Answer: null } => SindriErrors.InvalidInput.With("an answer needs one of --text, --file, --grant n, --grant-tokens n, --accept, --reject or --resume"),
        { Kind: ApplyCommandKind.Answer, Answer: AnswerVariant.Text, Text: null or "" } => SindriErrors.InvalidInput.With("--text needs the answer's text (or --file <file>)"),
        { Kind: ApplyCommandKind.Answer, Answer: AnswerVariant.Grant or AnswerVariant.GrantTokens, Amount: null or < 1 } => SindriErrors.InvalidInput.With("--grant and --grant-tokens need a whole number from 1"),
        { Text.Length: > IFactStore.MaxBytes } => SindriErrors.InvalidInput.With("the answer's text is larger than 1 MiB (fix: answer with a smaller file)"),
        { Kind: ApplyCommandKind.Cancel or ApplyCommandKind.Takeover or ApplyCommandKind.Handback } when request.Answer is not null || request.Text is not null
            || request.Amount is not null || request.Verb is not null || request.Challenge is not null =>
            SindriErrors.InvalidInput.With("cancel, takeover and handback take only the address and the nonce (fix: drop the answer, text, amount, verb and challenge fields)"),
        { Kind: ApplyCommandKind.Rewind, To: null or "" } => SindriErrors.InvalidInput.With("a rewind needs its target state: sindri workflow rewind <address> --to <state>"),
        { Kind: ApplyCommandKind.Rewind } when request.Answer is not null || request.Text is not null || request.Amount is not null || request.Verb is not null || request.Challenge is not null =>
            SindriErrors.InvalidInput.With("rewind takes only the address, --to and the nonce (fix: drop the answer, text, amount, verb and challenge fields)"),
        { Kind: not ApplyCommandKind.Rewind, To: not null } => SindriErrors.InvalidInput.With("only a rewind names a target state (fix: drop --to)"),
        { Kind: ApplyCommandKind.Command, Verb: null } => SindriErrors.InvalidInput.With("a command needs its verb: sindri workflow command <address> <verb>"),
        _ => null,
    };

    private static Trigger Trigger(ApplyCommandRequest request, string actor, CommandStamp stamp) => request.Kind switch
    {
        ApplyCommandKind.Answer => new AnswerGiven(actor, stamp, request.Answer!.Value, request.Text, request.Amount, Forwarded: null), // why: Malformed refuses an answer with no variant
        ApplyCommandKind.Command => new CommandFired(actor, request.Verb!.Value, stamp), // why: Malformed refuses a command with no verb
        ApplyCommandKind.Takeover => new TakeoverRequested(actor, stamp),
        ApplyCommandKind.Handback => new HandbackRequested(actor, stamp),
        ApplyCommandKind.Rewind => new RewindRequested(actor, stamp, request.To!), // why: Malformed refuses a rewind with no target
        ApplyCommandKind.Cancel or _ => new CancelRequested(actor, CancelOrigin.Command, stamp, Sequence: null, Parent: null),
    };

    /// <summary>The SHA-256 of the command's canonical arguments, so two different commands never share a nonce's subject.</summary>
    internal static string ArgumentsSha(ApplyCommandRequest request)
    {
        var text = string.Create(CultureInfo.InvariantCulture,
            $"kind {request.Kind}\nanswer {request.Answer}\namount {request.Amount}\nverb {request.Verb?.Value}\ntext {(request.Text is { } t ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(t))) : string.Empty)}\n{(request.To is { } to ? $"to {to}\n" : string.Empty)}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
