namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge eval …' and 'judge eval import-turns …': not in this build yet; cutover unit 6.3 replaces the body. Only
/// import-turns, spelled exactly, names a second word, so a mistyped 'eval import-turn' prints the one-word line.
/// </summary>
internal static class EvalCommand
{
    internal const string Name = "eval";
    internal const string ImportTurns = "import-turns";

    internal static int Run(IReadOnlyList<string> args) =>
        NotBuiltYet.Say(args.Count > 0 && string.Equals(args[0], ImportTurns, StringComparison.Ordinal) ? $"{Name} {ImportTurns}" : Name);
}
