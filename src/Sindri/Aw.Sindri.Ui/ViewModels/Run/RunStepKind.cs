namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Who does a step, as far as what is served says (F3 spec 3 §4: "Automatic" and "Only you" are not served yet, so no kind is invented).</summary>
internal enum RunStepKind
{
    /// <summary>Not served.</summary>
    Unknown,

    /// <summary>An agent ran it (a Step run with the agent runtime).</summary>
    Agent,

    /// <summary>It waits at the desk: you decide.</summary>
    You,

    /// <summary>It runs a sub-workflow.</summary>
    Sub,
}
