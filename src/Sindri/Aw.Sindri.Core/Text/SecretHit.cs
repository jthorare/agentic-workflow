namespace Aw.Sindri.Core.Text;

/// <summary>One secret the scan found in a text: the rule that matched and the matched value.</summary>
/// <param name="Rule">The rule id (for example 'secret-assignment' or 'github-token').</param>
/// <param name="Value">The matched value. Never log it.</param>
public sealed record SecretHit(string Rule, string Value);
