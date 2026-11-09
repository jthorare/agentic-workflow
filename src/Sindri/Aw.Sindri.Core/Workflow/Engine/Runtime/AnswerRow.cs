using System.Diagnostics;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>One answer variant as a command: its catalog action id, its class and the option that selects it; the single place those three are spelled.</summary>
/// <param name="Variant">The variant.</param>
/// <param name="Id">The catalog action id, without the 'ApplyCommand:' prefix.</param>
/// <param name="Class">The action class: grants and accept are protected, the rest desk.</param>
/// <param name="Option">The option text; a grant-tokens option carries the amount.</param>
internal sealed record AnswerRow(AnswerVariant Variant, string Id, ActionClass Class, string Option)
{
    private const string Prefix = "ApplyCommand:";

    /// <summary>The row of a variant.</summary>
    /// <param name="variant">The variant.</param>
    /// <param name="grantTokens">The amount a grant-tokens option carries.</param>
    /// <returns>The row.</returns>
    public static AnswerRow For(AnswerVariant variant, string grantTokens = "<n>") => variant switch
    {
        AnswerVariant.Text => new(variant, "answer-text", ActionClass.Desk, "--text <answer>"),
        AnswerVariant.Resume => new(variant, "answer-resume", ActionClass.Desk, "--resume"),
        AnswerVariant.Reject => new(variant, "answer-reject", ActionClass.Desk, "--reject"),
        AnswerVariant.Grant => new(variant, "answer-grant", ActionClass.Desk, "--grant 1"),
        AnswerVariant.GrantTokens => new(variant, "answer-grant-tokens", ActionClass.Desk, $"--grant-tokens {grantTokens}"),
        AnswerVariant.Accept => new(variant, "answer-accept", ActionClass.Desk, "--accept"),
        _ => throw new UnreachableException($"no answer row for {variant}"),
    };

    /// <summary>The row of a catalog action id such as 'ApplyCommand:answer-grant'.</summary>
    /// <param name="actionId">The action id.</param>
    /// <returns>The row.</returns>
    public static AnswerRow ForAction(string actionId) =>
        Enum.GetValues<AnswerVariant>().Select(v => For(v)).FirstOrDefault(r => string.Equals(Prefix + r.Id, actionId, StringComparison.Ordinal))
        ?? throw new UnreachableException($"{actionId} is not an answer action");

    /// <summary>The catalog action id with its prefix.</summary>
    public string Action => Prefix + Id;

    /// <summary>The command that runs this answer.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>'sindri workflow answer &lt;address&gt; &lt;option&gt;'.</returns>
    public string Cli(string address) => $"sindri workflow answer {address} {Option}";
}
