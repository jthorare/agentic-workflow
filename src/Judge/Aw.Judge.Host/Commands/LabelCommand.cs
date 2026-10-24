namespace Aw.Judge.Host.Commands;

/// <summary>'judge label …' (import, outcomes, set, status, or nothing): not in this build yet; cutover unit 6.3 replaces the body.</summary>
internal static class LabelCommand
{
    internal const string Name = "label";

    internal static int Run() => NotBuiltYet.Say(Name);
}
