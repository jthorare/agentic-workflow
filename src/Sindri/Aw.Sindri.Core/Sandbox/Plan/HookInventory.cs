namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The closed inventory of the hooks that run in a session and the state paths they need (sandbox spec §3.1).</summary>
public static class HookInventory
{
    /// <summary>The state-directory exceptions, taken from the installed hooks (config/hooks, lib/failopen.sh, the judge wrapper and bin-r1b/heavy).</summary>
    public static IReadOnlyList<StatePathEntry> StatePaths { get; } =
    [
        new("judge", StatePathKind.ReadWrite, "done-gate, external-write-guard, turn-origin, send-gate, outbox-flush, record-teammate-name, subagent-start-map, judge: decisions.sqlite with its -wal and -shm, sessions, outbox, teammate names"),
        new("judge/config.json", StatePathKind.ReadOnly, "judge: the configuration stays the owner's"),
        new("logs", StatePathKind.PerLaunchDir, "lib/failopen.sh appends failopen.log; the launcher reads the per-launch log into the run's denial summary"),
        new("probe", StatePathKind.PerLaunchDir, "probe-log writes the hook-input probe"),
        new("digests", StatePathKind.PerLaunchDir, "context-guard writes digests; a Step can't read other sessions' excerpts"),
        new("context-guard", StatePathKind.ReadOnly, "context-guard reads its configuration"),
        new("context-guard/state", StatePathKind.ReadWrite, "context-guard writes per-session state"),
        new("locks", StatePathKind.ReadWrite, "heavy: mkdir mutexes, heavy-job.lock and heavy-session"),
        new("heavy-slots-cap", StatePathKind.ReadOnly, "heavy reads the box-wide cap"),
        new("heavy-meta-reserve", StatePathKind.ReadOnly, "heavy reads the meta reserve"),
        new("bin-r1b", StatePathKind.ReadOnly, "the heavy wrapper"),
        new("meta", StatePathKind.Absent, "meta-outbox, meta-stop-gate and meta-lane-gate do nothing without it, by design"),
        new("bin/aw-meta", StatePathKind.Absent, "meta hooks exit at once when aw-meta is not executable"),
    ];

    /// <summary>
    /// Every owner hook a session would run, with its treatment (owner, 2026-10-18): keep is the default; a hook is dropped only when it answers
    /// 'allow' in a way that bypasses 'dontAsk', and a hook that rewrites commands is kept with its rewritten forms denied too.
    /// <see cref="HookPermissionPolicy"/> applies it; a hook missing here is kept and noted in the plan.
    /// </summary>
    public static IReadOnlyList<HookScript> Scripts { get; } =
    [
        Keep("block-destructive", "PreToolUse Bash", "denies only (exit 2): a guard that tightens"),
        Keep("block-push-main", "PreToolUse Bash", "denies only (exit 2): a guard that tightens"),
        Keep("context-guard", "PostToolUse *", "adds context and writes digests; answers nothing"),
        Keep("detect-secrets", "PreToolUse Bash", "denies only (exit 2): a guard that tightens"),
        Keep("done-gate", "Stop", "blocks a stop (exit 2) until the brief's proof is shown; answers no permission"),
        Keep("done-gate-annotate", "PostToolUse Agent", "annotates the result; never blocks or answers"),
        new("external-write-guard", Hook("external-write-guard"), "PreToolUse *", AnswersPermissions: true, RewritesInput: false, HookTreatment.Drop,
            "answers 'allow' to every tool call it doesn't match, so with it every tool would pass 'dontAsk'; its external writes stay refused by the deny rule 'gh:*' and by 'dontAsk' for tools outside the Step's list"),
        Keep("git-context", "SessionStart", "prints git context; answers nothing"),
        Keep("judge-health", "SessionStart", "warns when the judge is degraded; answers nothing"),
        Keep("meta-lane-gate", "UserPromptSubmit, SessionStart, PostToolUse, Stop (Codex and Cursor lanes)", "acts only in a Meta lane (does nothing in a Sindri launch); blocks a stop, answers no permission"),
        Keep("meta-outbox", "PostToolUse SendMessage", "records sent messages for the meta; answers nothing"),
        Keep("meta-stop-gate", "Stop", "blocks only the registered meta's stop; answers no permission"),
        Keep("outbox-flush", "TeammateIdle", "keeps a teammate working (exit 2); answers no permission"),
        Keep("prism-context", "SessionStart", "loads memory context; answers nothing"),
        Keep("probe-log", "every event, while the probe is on", "logs the hook input; prints nothing"),
        Keep("prompt-sort", "UserPromptSubmit", "may print a scaffold note; answers nothing"),
        new("record-teammate-name", Hook("record-teammate-name"), "PreToolUse Agent", AnswersPermissions: true, RewritesInput: false, HookTreatment.Drop,
            "always answers 'allow' to Agent, so every dispatch would pass 'dontAsk'; without it subagent-start-map uses its dispatch-order fallback"),
        new("rtk-rewrite", Hook("rtk-rewrite"), "PreToolUse Bash", AnswersPermissions: true, RewritesInput: true, HookTreatment.KeepDenyRewrites,
            "rewrites commands to their 'rtk' form for token savings (the owner keeps it); every denied command is also denied in its 'rtk' form, so 'git push' can't pass as 'rtk git push'",
            RewritePrefix: "rtk "),
        new("scope-gate", Hook("scope-gate"), "PreToolUse Agent", AnswersPermissions: true, RewritesInput: false, HookTreatment.Drop,
            "answers 'allow' to an Agent dispatch with a good brief, so dispatches would pass 'dontAsk'"),
        new("send-gate", Hook("send-gate"), "PreToolUse SendMessage", AnswersPermissions: true, RewritesInput: true, HookTreatment.Drop,
            "answers 'allow' to SendMessage (and rewrites the message), so messages would pass 'dontAsk'"),
        Keep("session-id-report", "SessionStart (Codex and Cursor)", "reports the session id to Sindri's launcher; answers nothing"),
        Keep("sindri-nudge", "SessionStart", "prints a reminder; answers nothing"),
        Keep("subagent-start-map", "SubagentStart", "maps a subagent to its brief; answers nothing"),
        Keep("turn-origin", "UserPromptSubmit", "marks the turn's origin; answers nothing"),
        new("winch", "config/settings.json", "PreToolUse *, Stop (inline)", AnswersPermissions: false, RewritesInput: false, HookTreatment.Keep,
            "asks the owner's terminal to redraw; answers nothing"),
        new("aw-live", "mods/aw-live", "in-process mod (the /live pane)", AnswersPermissions: false, RewritesInput: false, HookTreatment.Keep,
            "draws this session's numbers; answers nothing"),
    ];

    private static HookScript Keep(string name, string events, string why) =>
        new(name, Hook(name), events, AnswersPermissions: false, RewritesInput: false, HookTreatment.Keep, why);

    private static string Hook(string name) => "config/hooks/" + name + ".sh";

    /// <summary>The inventory entry for a hook, or null when the inventory doesn't list it.</summary>
    /// <param name="name">The hook's name.</param>
    /// <returns>The entry.</returns>
    public static HookScript? Find(string name) => Scripts.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.Ordinal));
}
