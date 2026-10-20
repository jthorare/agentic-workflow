using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>Builds the needs-you rows (P4 plan §1.3): pure, every free-text field through <see cref="SafeText"/>, every CLI line through CliForm.Fill.</summary>
public static partial class NeedsYouRows
{
    private const int TitleMax = 120;
    private const int ReasonMax = 300;
    private const int PathsShown = 3;
    private static readonly ActionId _decide = ActionId.From("Decide");
    private static readonly ActionId _runService = ActionId.From("RunService");
    private static readonly ActionId _pauseService = ActionId.From("PauseService");
    private static readonly ActionId _approveProfile = ActionId.From("ApproveProfile");

    /// <summary>The row for a pending decision.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForDecision(HumanDecisionRow decision, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = Cli("sindri decide <decisionId> <choice>", "decisionId", decision.Id).Bind(form => form.Fill("choice", DecisionKinds.Recommended(decision.Kind)));
        if (cli.IsError)
        {
            return cli.Error;
        }

        var recordedOnly = RequestRules.RecordedOnly();
        var firstLine = decision.Text.Split(['\r', '\n'], 2)[0];
        return new NeedsYouRow(
            NeedsYouRowId.From("decision:" + decision.Id.Value),
            NeedsYouKind.Decision,
            DecisionLabels.For(decision.Kind, decision.RequestKind),
            new NeedsYouSubject(SubjectKind.Decision, SubjectId.From(decision.Id.Value)),
            decision.Item,
            SafeText.Clean(scrubber, firstLine, TitleMax),
            SafeText.Clean(scrubber, $"{RequestKindWords.Of(decision.RequestKind)} request from {decision.FiledBy}", ReasonMax),
            decision.Repo,
            Urgency.Pulse,
            decision.FiledAt,
            recordedOnly,
            recordedOnly ? EmptyText.RecordedOnly() : null,
            AnswerKind: null,
            _decide,
            [_decide],
            cli.Value.Text);
    }

    /// <summary>The row for a service whose last run failed.</summary>
    /// <param name="service">The service's row.</param>
    /// <param name="since">When it started to fail.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <param name="builtIn">True for a built-in service, which can't be paused: its row offers RunService only.</param>
    /// <returns>The row, or SND-CLI-003 when the service's last run didn't fail.</returns>
    public static Result<NeedsYouRow> ForFailure(ServiceRow service, DateTimeOffset since, ITextScrubber scrubber, bool builtIn = false)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(scrubber);
        if (service.LastRun is not { Outcome: ServiceOutcome.Failed } last)
        {
            return SindriErrors.InvalidInput.With($"{service.Name.Value}'s last run didn't fail, so it has no failure row");
        }

        var name = service.Name.Value;
        var unknown = string.Equals(last.Code, SindriErrors.ServiceNotFound.Code, StringComparison.Ordinal);
        var cli = unknown ? CliForm.Parse("sindri profile validate") : Cli("sindri services run <name>", "name", service.Name);
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowId.From("failure:service:" + name),
            NeedsYouKind.Failure,
            "failure · " + name,
            new NeedsYouSubject(SubjectKind.Service, SubjectId.From(name)),
            Item: null,
            name + " failed",
            unknown ? UnknownReason(name, scrubber) : Reason(last, scrubber),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            unknown ? null : _runService,
            unknown ? [_pauseService] : builtIn ? [_runService] : [_runService, _pauseService],   // a built-in can't be paused
            cli.Value.Text);
    }

    /// <summary>The row for a live profile that drifted from the approved one.</summary>
    /// <param name="profile">The profile row.</param>
    /// <param name="since">When it started to drift.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row, or null when the profile hasn't drifted or its live hash is unknown.</returns>
    public static Result<NeedsYouRow?> ForProfile(ProfileRow profile, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(scrubber);
        if (!profile.Changed || profile.Live is not { } live)
        {
            return Results.Success<NeedsYouRow?>(value: null);
        }

        var cli = Cli("sindri profile approve <hash>", "hash", live);
        if (cli.IsError)
        {
            return cli.Error;
        }

        return Results.Success<NeedsYouRow?>(new NeedsYouRow(
            NeedsYouRowId.From("profile:drift"),
            NeedsYouKind.Profile,
            "profile · drifted",
            new NeedsYouSubject(SubjectKind.Profile, SubjectId.From(live.Value)),
            Item: null,
            "The profile changed since approval",
            PathsReason(profile.ChangedPaths, scrubber),
            Repo: null,
            Urgency.Calm,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            _approveProfile,
            [_approveProfile],
            cli.Value.Text));
    }

    private static Result<CliForm> Cli<TId>(string form, string placeholder, TId id)
        where TId : struct, Kernel.Ids.ITypedId<TId, string> =>
        CliForm.Parse(form).Bind(parsed => parsed.Fill(placeholder, id));

    private static string UnknownReason(string name, ITextScrubber scrubber) =>
        SafeText.Clean(scrubber, $"{SindriErrors.ServiceNotFound.Code}: this build has no service '{name}'; remove or rename it in the profile", ReasonMax);

    private static string Reason(LastRun last, ITextScrubber scrubber)
    {
        if (last.Code is not { Length: > 0 } code)
        {
            return "failed at " + last.Slot.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        var known = SindriErrors.All.FirstOrDefault(e => string.Equals(e.Code, code, StringComparison.Ordinal));
        return SafeText.Clean(scrubber, known is null ? code : $"{code}: {known.Title}", ReasonMax);
    }

    private static string PathsReason(IReadOnlyList<string> paths, ITextScrubber scrubber)
    {
        var count = paths.Count;
        var shown = string.Join(", ", paths.Take(PathsShown).Select(p => SafeText.Clean(scrubber, p, ReasonMax)));
        var noun = count == 1 ? "key path" : "key paths";
        return string.Create(CultureInfo.InvariantCulture, $"{count} {noun} changed: {shown}");
    }
}
