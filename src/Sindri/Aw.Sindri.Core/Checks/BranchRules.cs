namespace Aw.Sindri.Core.Checks;

/// <summary>The build rules that read a branch's changes (line spec §7.2): the scope gate, the test rule and the commit rule. The secret scan is <see cref="SecretScan"/>.</summary>
public static class BranchRules
{
    /// <summary>The rule id of the scope gate.</summary>
    public const string Scope = "BLD-SCOPE-001";

    /// <summary>The rule id of the secret scan.</summary>
    public const string Secret = "BLD-SECRET-001";

    /// <summary>The rule id of the existing-test rule.</summary>
    public const string Test = "BLD-TEST-001";

    /// <summary>The rule id of the commit rule.</summary>
    public const string Commit = "BLD-COMMIT-001";

    private static readonly string[] _commitTypes = ["feat", "fix", "refactor", "test", "docs", "chore", "perf", "build", "ci", "style", "revert"];

    /// <summary>BLD-SCOPE-001: every changed path matches 'allowedFiles' or is a new test file by convention.</summary>
    /// <param name="scope">The phase packet in force.</param>
    /// <param name="changes">What the branch changed.</param>
    /// <returns>One finding per path outside the scope.</returns>
    public static IReadOnlyList<RuleFinding> OutOfScope(PhaseScope scope, BranchChanges changes)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(changes);
        return [.. changes.Files
            .Where(f => !ScopeGlobs.Allows(scope.AllowedFiles, f.Path) && !(f.Kind == ChangeKind.Added && TestPaths.IsTest(f.Path)))
            .Select(static f => new RuleFinding(Scope, $"{f.Path} is outside the phase's allowedFiles (the owner can allow it at the scope desk, or it can be reverted)", f.Path))];
    }

    /// <summary>BLD-TEST-001: no existing test file is changed or deleted unless 'testChange' names it.</summary>
    /// <param name="scope">The phase packet in force.</param>
    /// <param name="changes">What the branch changed.</param>
    /// <returns>One finding per test file changed without a reason.</returns>
    public static IReadOnlyList<RuleFinding> ChangedTests(PhaseScope scope, BranchChanges changes)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(changes);
        return [.. changes.Files
            .Where(f => f.Kind != ChangeKind.Added && TestPaths.IsTest(f.Path) && !scope.TestChange.Contains(f.Path, StringComparer.Ordinal))
            .Select(static f => new RuleFinding(Test, $"the existing test {f.Path} was {(f.Kind == ChangeKind.Deleted ? "deleted" : "changed")} but the phase's testChange doesn't name it", f.Path))];
    }

    /// <summary>BLD-COMMIT-001: commits are conventional ('type: description') and none is a merge.</summary>
    /// <param name="changes">What the branch changed.</param>
    /// <returns>One finding per commit that is not.</returns>
    public static IReadOnlyList<RuleFinding> Commits(BranchChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return [.. changes.Commits.Select(Judge).OfType<RuleFinding>()];
    }

    /// <summary>The type of a conventional subject ('feat' of 'feat(x): add'), or null when the subject is not one.</summary>
    /// <param name="subject">The commit subject.</param>
    /// <returns>The type.</returns>
    public static string? CommitType(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var colon = subject.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 1 || colon + 2 >= subject.Length)
        {
            return null;
        }

        var head = subject[..colon].TrimEnd('!');
        var open = head.IndexOf('(', StringComparison.Ordinal);
        var type = open < 0 ? head : head[..open];
        var scopeOk = open < 0 || (head[^1] == ')' && open + 1 < head.Length - 1);
        return scopeOk && _commitTypes.Contains(type, StringComparer.Ordinal) ? type : null;
    }

    private static RuleFinding? Judge(ChangedCommit commit)
    {
        var shortSha = commit.Sha.Length > 8 ? commit.Sha[..8] : commit.Sha;
        if (commit.IsMerge)
        {
            return new RuleFinding(Commit, $"{shortSha} is a merge commit; the branch must be a straight line of atomic commits");
        }

        return CommitType(commit.Subject) is null
            ? new RuleFinding(Commit, $"{shortSha} '{commit.Subject}' isn't 'type: description' (types: {string.Join(", ", _commitTypes)})")
            : null;
    }
}
