namespace Aw.Sindri.Core.Setup;

/// <summary>One onboarded repo's pre-commit hook, as the doctor's 'pre-commit:&lt;repo&gt;' line reports it.</summary>
/// <param name="Repo">The repo's name in the profile.</param>
/// <param name="Path">The repo's checkout.</param>
/// <param name="Kind">What the repo's pre-commit file is.</param>
public sealed record RepoHookState(string Repo, string Path, HookKind Kind);
