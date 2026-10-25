using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Doctor's 'setup' check (setup-once spec §7.5): ok when every step is done or skipped; otherwise a warning that names each step still open and the owner's next move, the first open step's terminal form.
/// Its companions warn and never fail: 'pre-commit:&lt;repo&gt;' per onboarded repo (the secret-scan hook is missing, the TS build's, or someone else's; cutover spec §6.2), 'setup:repo-hook' (kept only in a host with no hook reader) and 'setup:recipes-tree' (the embedded recipe set's tree hash differs from the checkout's HEAD, §5.3).
/// Never a failure: an unfinished setup is not a broken Sindri.
/// </summary>
/// <param name="facts">The sources, read the way GET /setup reads them.</param>
/// <param name="build">The embedded recipe set's tree hash and HEAD's; absent in a host with no build stamp.</param>
/// <param name="hooks">Each repo's pre-commit hook; absent keeps the single 'setup:repo-hook' warning.</param>
internal sealed class SetupDoctor(ISetupFacts facts, IRecipeBuild? build = null, IRepoHookStates? hooks = null) : IDoctorChecks
{
    /// <summary>The check's name.</summary>
    internal const string Name = "setup";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        List<DoctorCheck> checks = [await Steps(cancellationToken).ConfigureAwait(false)];
        if (hooks is not null)
        {
            checks.AddRange((await hooks.Read(cancellationToken).ConfigureAwait(false)).Select(HookLine));
        }
        else if (await facts.Repos(cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value.Count: > 0 })
        {
            checks.Add(new DoctorCheck("setup:repo-hook", CheckState.Warn, "No secret-check hook yet: add it when the installer ships", "add a pre-commit hook that refuses secret-shaped text to each repo by hand"));
        }

        if (build is not null && await build.Read(cancellationToken).ConfigureAwait(false) is { Head: { } head } trees)
        {
            checks.Add(string.Equals(trees.Embedded, head, StringComparison.Ordinal)
                ? new DoctorCheck("setup:recipes-tree", CheckState.Ok, "the embedded recipe set is HEAD's", Fix: null)
                : new DoctorCheck("setup:recipes-tree", CheckState.Warn, $"the embedded recipe set ({trees.Embedded}) is not this checkout's HEAD ({head})", "scripts/install-cs.sh --rebuild"));
        }

        return checks;
    }

    // One line per onboarded repo (cutover spec §6.2 item 2): ok with the v3 hook, else a warning whose fix installs it (a foreign hook is never overwritten).
    private static DoctorCheck HookLine(RepoHookState state)
    {
        var name = "pre-commit:" + state.Repo;
        return state.Kind switch
        {
            HookKind.V3 => new DoctorCheck(name, CheckState.Ok, "the secret-scan pre-commit hook is installed", Fix: null),
            HookKind.Foreign => new DoctorCheck(name, CheckState.Warn, "a pre-commit hook that is not Sindri's is installed, so staged secrets are not scanned", $"add 'sindri scrub --staged || exit 1' to {state.Path}'s pre-commit hook by hand"),
            HookKind.None => new DoctorCheck(name, CheckState.Warn, "no pre-commit hook, so staged secrets are not scanned", Install(state)),
            HookKind.V2 or HookKind.V1 => new DoctorCheck(name, CheckState.Warn, "the pre-commit hook is the TS build's, which Sindri replaces", Install(state)),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static string Install(RepoHookState state) => "sindri scrub --install-pre-commit --repo " + state.Path;

    private async ValueTask<DoctorCheck> Steps(CancellationToken cancellationToken)
    {
        var steps = await new SetupDeriver(facts).Derive(cancellationToken).ConfigureAwait(false);
        var open = steps.Where(static s => s.State is not (SetupState.Done or SetupState.Skipped)).ToList();
        if (open.Count == 0)
        {
            return new DoctorCheck(Name, CheckState.Ok, "every step is done", Fix: null);
        }

        var detail = "not finished: " + string.Join(", ", open.Select(static s => $"{SetupStepChanges.Word(s.Id)} ({SetupStepChanges.Word(s.State)})"));
        return new DoctorCheck(Name, CheckState.Warn, detail, Fix: FixFor(open[0].Id));
    }

    // The terminal form of the action that moves a step forward; the app's button does the same.
    private static string FixFor(SetupStepId id) => id switch
    {
        SetupStepId.Repo => "sindri repo add <path>",
        SetupStepId.Profile => "sindri profile approve <hash>",
        SetupStepId.Recipes => "sindri recipe approve-all",
        SetupStepId.Mcp => "sindri mcp register --apply",
        SetupStepId.Map => "sindri context map refresh",
        SetupStepId.Onyx => "sindri onyx up",
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };
}
