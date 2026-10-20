namespace Aw.Sindri.Core.Packets;

/// <summary>One check of a packet that failed.</summary>
/// <param name="Rule">The rule id, for example 'PKT-GRAPH-002'.</param>
/// <param name="Subject">What it is about: 'packet', 'task t1'.</param>
/// <param name="Message">What was found.</param>
/// <param name="Fix">How to fix it; null when the tool gave none.</param>
public sealed record PacketFailure(string Rule, string Subject, string Message, string? Fix);
