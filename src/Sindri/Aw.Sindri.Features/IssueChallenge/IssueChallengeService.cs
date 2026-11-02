using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>
/// Issues a challenge for one protected action (engine spec §4.2, S1-D5): parses the action text, plans the action so its
/// subject and preview are the server's own, and returns the challenge with that preview. The chain lets only a human at
/// a terminal reach it. Forms: 'approve &lt;id&gt; &lt;version&gt;' (or 'approve &lt;id&gt;@&lt;hash&gt;', issued as the
/// former), 'revoke &lt;id&gt;@&lt;hash&gt;' and
/// 'approve profile &lt;hash6&gt;' (the profile's preview is profile approve's own, so that one carries none), and a
/// protected answer on an instance, 'grant &lt;n&gt; &lt;address&gt;', 'grant-tokens &lt;n&gt; &lt;address&gt;' or 'accept
/// &lt;address&gt;' (engine spec §6.2), bound to the instance's address and version, whose preview is the instance.
/// </summary>
/// <param name="approvals">Plans a recipe approval, registering a shipped version that waits for its own approval first (#983).</param>
/// <param name="revokes">Plans a revoke.</param>
/// <param name="challenges">The server's challenges.</param>
/// <param name="profiles">The live profile, for 'approve profile'.</param>
/// <param name="directory">Resolves an instance's address, for a protected answer.</param>
/// <param name="runtime">Reads the instance as it is now, for a protected answer.</param>
/// <param name="approveAll">Reads the approve-all plan, for 'approve-all &lt;digest6&gt;'.</param>
internal sealed partial class IssueChallengeService(ShippedApprovalPlans approvals, RecipeRevokePlanner revokes, IChallenges challenges, IProfileSource profiles,
    IInstanceDirectory directory, IInstanceRuntime runtime, ApproveAllPlanReader? approveAll = null)
    : IService<IssueChallengeRequest, IssueChallengeResponse>
{
    private const string Forms = "'approve <recipe> <version>', 'revoke <recipe>@<hash>', 'approve profile <hash6>', 'approve-all <digest6>', 'grant <n> <address>', 'grant-tokens <n> <address>' or 'accept <address>'";

    public ValueTask<Result<IssueChallengeResponse>> Handle(IssueChallengeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Issue(request.Action, ChallengeChannel.Terminal, cancellationToken);
    }

    /// <summary>Issues the challenge for <paramref name="action"/> on <paramref name="channel"/>: the terminal's route and the app's (setup-once spec §4) plan and preview alike.</summary>
    /// <param name="action">The action text.</param>
    /// <param name="channel">Where the challenge is answered. The app's approve-all is held; every other challenge is typed.</param>
    /// <param name="cancellationToken">Cancels the issue.</param>
    /// <returns>The challenge and its preview.</returns>
    internal async ValueTask<Result<IssueChallengeResponse>> Issue(string action, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        var text = action.Trim();
        return ProfileForm.Match(text) is { Success: true } profile ? await Profile(profile.Groups["hash"].Value, channel, cancellationToken).ConfigureAwait(false)
            : ApproveAllForm.Match(text) is { Success: true } all ? await ApproveAll(all.Groups["digest"].Value, channel, cancellationToken).ConfigureAwait(false)
            : ApproveForm.Match(text) is { Success: true } approve ? await Approve(text, approve, channel, cancellationToken).ConfigureAwait(false)
            : ApproveByHashForm.Match(text) is { Success: true } byHash ? await ApproveByHash(text, byHash, channel, cancellationToken).ConfigureAwait(false)
            : RevokeForm.Match(text) is { Success: true } revoke ? await Revoke(text, revoke, cancellationToken).ConfigureAwait(false)
            : AnswerForm.Match(text) is { Success: true } answer ? await Answer(text, answer, cancellationToken).ConfigureAwait(false)
            : Unknown(text);
    }

    private async ValueTask<Result<IssueChallengeResponse>> Approve(string text, Match form, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        if (!RecipeId.TryFrom(form.Groups["id"].Value, out var id)
            || !int.TryParse(form.Groups["version"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return Unknown(text);
        }

        var plan = await approvals.Approve(id, version, cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : Issued(Mint(plan.Value.Action, plan.Value.Subject, channel), plan.Value.Preview, revoke: null);
    }

    private async ValueTask<Result<IssueChallengeResponse>> ApproveByHash(string text, Match form, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        if (!RecipeId.TryFrom(form.Groups["id"].Value, out var id))
        {
            return Unknown(text);
        }

        var plan = await approvals.Approve(id, form.Groups["hash"].Value, cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : Issued(Mint(plan.Value.Action, plan.Value.Subject, channel), plan.Value.Preview, revoke: null);
    }

    private async ValueTask<Result<IssueChallengeResponse>> Revoke(string text, Match form, CancellationToken cancellationToken)
    {
        if (!RecipeId.TryFrom(form.Groups["id"].Value, out var id))
        {
            return Unknown(text);
        }

        var plan = await revokes.Revoke(id, form.Groups["hash"].Value, cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : Issued(challenges.Issue(plan.Value.Action, plan.Value.Subject), approve: null, plan.Value.Preview);
    }

    /// <summary>A protected answer's challenge: the instance as it is now is both its subject (address and version) and its preview.</summary>
    private async ValueTask<Result<IssueChallengeResponse>> Answer(string text, Match form, CancellationToken cancellationToken)
    {
        var verb = form.Groups["verb"].Value;
        int? amount = form.Groups["n"].Success && int.TryParse(form.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        if (verb != "accept" && amount is null)
        {
            return Unknown(text);
        }

        var address = await directory.Resolve(form.Groups["address"].Value, cancellationToken).ConfigureAwait(false);
        var view = address.IsError ? address.Error : await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var variant = verb switch { "grant" => AnswerVariant.Grant, "grant-tokens" => AnswerVariant.GrantTokens, _ => AnswerVariant.Accept };
        var bound = await BoundTo(address.Value, view.Value, cancellationToken).ConfigureAwait(false);
        if (bound.IsError)
        {
            return bound.Error;
        }

        var issued = challenges.Issue(InstanceChallenge.Action(bound.Value.Address, variant, amount), InstanceChallenge.Subject(bound.Value.View));
        return Issued(issued, approve: null, revoke: null) with { Instance = view.Value };
    }

    /// <summary>
    /// The instance a protected answer's challenge is bound to: the one the answer is delivered to. An answer to a parent whose open park mirrors
    /// a child is forwarded to the child (engine spec §5.8), and ApplyCommand redeems against the child's address and version, so the challenge
    /// is issued for the child (the text the human types names it).
    /// </summary>
    private async ValueTask<Result<(InstanceAddress Address, InstanceView View)>> BoundTo(InstanceAddress address, InstanceView view, CancellationToken cancellationToken)
    {
        if (view.ChildPark is not { } child)
        {
            return (address, view);
        }

        var childView = await runtime.Get(child.Address, cancellationToken).ConfigureAwait(false);
        return childView.IsError ? childView.Error : (child.Address, childView.Value);
    }

    private static Error Unknown(string text) => SindriErrors.InvalidInput.With($"'{text}' isn't an action a challenge is issued for; use {Forms}");

    /// <summary>An approve-all's challenge (setup-once spec §4 item 1): bound to the plan's digest, with the plan as its preview. Refused before a profile is approved (SND-SETUP-007), when nothing waits, or when the digest is not the plan's.</summary>
    private async ValueTask<Result<IssueChallengeResponse>> ApproveAll(string digest, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        if (approveAll is null)
        {
            return Unknown("approve-all " + digest);
        }

        var plan = await approveAll.Read(cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        return plan.Value.NothingToHold ? SindriErrors.InvalidInput.With("There is nothing for one hold to approve (a recipe that needs its own approval is approved one at a time).")
            : !string.Equals(plan.Value.Digest, digest, StringComparison.Ordinal) ? WorkflowErrors.RecipeChallengeStale.With($"{digest} isn't the plan's digest ({plan.Value.Digest}); read the plan again")
            : Issued(Mint(ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest), channel), approve: null, revoke: null) with { ApproveAll = plan.Value };
    }

    private async ValueTask<Result<IssueChallengeResponse>> Profile(string hash6, ChallengeChannel channel, CancellationToken cancellationToken)
    {
        var live = await profiles.Load(cancellationToken).ConfigureAwait(false);
        if (live.IsError)
        {
            return live.Error;
        }

        var hash = live.Value.Hash;
        return string.Equals(hash.Abbreviated, hash6, StringComparison.Ordinal)
            ? Issued(Mint(ChallengeAction.ApproveProfile(hash), ChallengeSubject.ForProfile(hash), channel), approve: null, revoke: null)
            : SindriErrors.ProfileHashMismatch.With($"{hash6} isn't the live profile's hash; the live hash is {hash.Value} (fix: sindri profile approve {hash.Value})");
    }

    /// <summary>Issues the challenge: at the terminal it is typed; in the app approve-all is held (typing its text also redeems it) and everything else is typed.</summary>
    private IssuedChallenge Mint(ChallengeAction action, ChallengeSubject subject, ChallengeChannel channel) =>
        challenges.Issue(action, subject, channel == ChallengeChannel.App && action.Text.StartsWith("approve-all ", StringComparison.Ordinal) ? ChallengeMode.Held : ChallengeMode.Typed, channel);

    private static IssueChallengeResponse Issued(IssuedChallenge issued, ApprovalPreview? approve, RevokePreview? revoke)
    {
        var response = new IssueChallengeResponse(issued.Id, issued.Action.Text, issued.ExpiresAt, approve, revoke);
        if (issued.Channel != ChallengeChannel.App)
        {
            return response;
        }

        var held = issued.Mode == ChallengeMode.Held;
        return response with { Confirm = held ? "held" : "typed", MinHoldMs = held ? IssuedChallenge.MinHoldMs : null, TypedAlternative = held ? issued.Action.Text : null };
    }

    [GeneratedRegex(@"^approve-all (?<digest>[0-9a-f]{6})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApproveAllForm { get; }

    [GeneratedRegex(@"^approve profile (?<hash>[0-9a-f]{6})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ProfileForm { get; }

    [GeneratedRegex(@"^approve (?<id>\S+) (?<version>[0-9]{1,9})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApproveForm { get; }

    [GeneratedRegex(@"^approve (?<id>[^@\s]+)@(?<hash>(sha256:)?[0-9a-f]{8,64})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApproveByHashForm { get; }

    [GeneratedRegex(@"^(?<verb>grant|grant-tokens) (?<n>[0-9]{1,9}) (?<address>\S+)\z|^(?<verb>accept) (?<address>\S+)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnswerForm { get; }

    [GeneratedRegex(@"^revoke (?<id>[^@\s]+)@(?<hash>(sha256:)?[0-9a-f]{8,64})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RevokeForm { get; }
}
