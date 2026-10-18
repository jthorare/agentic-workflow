namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One git setting delivered at command-line scope.</summary>
/// <param name="Key">The key, for example 'commit.gpgsign'.</param>
/// <param name="Value">The value.</param>
public sealed record GitSetting(string Key, string Value);
