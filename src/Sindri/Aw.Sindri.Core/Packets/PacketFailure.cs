namespace Aw.Sindri.Core.Packets;

/// <summary>One check of a packet that failed.</summary>
/// <param name="Rule">The rule id, for example 'PKT-GRAPH-002'.</param>
/// <param name="Subject">What it is about: 'packet', 'task t1'.</param>
/// <param name="Message">What was found.</param>
/// <param name="Fix">How to fix it; null when the tool gave none.</param>
public sealed record PacketFailure(string Rule, string Subject, string Message, string? Fix)
{
    /// <summary>The failures as one line for a message: each rule and what it found.</summary>
    /// <param name="failures">The failed checks.</param>
    /// <returns>'RULE: message; RULE: message'.</returns>
    public static string Describe(IEnumerable<PacketFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return string.Join("; ", failures.Select(static f => $"{f.Rule}: {f.Message}"));
    }
}
