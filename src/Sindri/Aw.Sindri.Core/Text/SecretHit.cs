namespace Aw.Sindri.Core.Text;

/// <summary>One secret the scan found in a text: the rule that matched, the name the value is assigned to, and the matched value.</summary>
/// <param name="Rule">The rule id (for example 'secret-assignment' or 'github-token').</param>
/// <param name="Key">The name right before the value (ESO_SECRET in ESO_SECRET=...); empty when the rule has none.</param>
/// <param name="Value">The matched value. Never log it.</param>
public sealed record SecretHit(string Rule, string Key, string Value);
