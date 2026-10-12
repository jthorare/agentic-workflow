namespace Aw.Judge.Features.SetConfig;

/// <summary>The SetConfig request ('judge config set &lt;question&gt; enabled true|false | threshold &lt;0-1&gt;'): exactly one of
/// <paramref name="Enabled"/> and <paramref name="Threshold"/> is set.</summary>
/// <param name="Question">The question name.</param>
/// <param name="Enabled">The new enabled flag, or null.</param>
/// <param name="Threshold">The new threshold, or null.</param>
public sealed record SetConfigRequest(string Question, bool? Enabled = null, double? Threshold = null);
