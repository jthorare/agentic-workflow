using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the recipe sheet (screens spec §4.3). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The sheet's note when a re-read no longer finds the recipe it shows.</summary>
    internal const string RecipeGone = "This workflow is no longer served; what is shown was its last state.";

    /// <summary>A recipe hash as the CLI shows it: its first twelve hex digits, without the scheme (the API serves the whole hash on a recipe).</summary>
    /// <param name="hash">The hash as served, sha256: and 64 hex digits.</param>
    /// <returns>Twelve hex digits.</returns>
    internal static string Hash12(string hash)
    {
        var digits = hash.StartsWith("sha256:", StringComparison.Ordinal) ? hash["sha256:".Length..] : hash;
        return digits.Length > 12 ? digits[..12] : digits;
    }

    /// <summary>A recipe's urgency in the word the API serves it in.</summary>
    /// <param name="urgency">The urgency.</param>
    /// <returns>The word.</returns>
    internal static string UrgencyWord(RecipeUrgency urgency) => urgency switch
    {
        RecipeUrgency.Incident => "incident",
        RecipeUrgency.Triage => "triage",
        RecipeUrgency.Work => "work",
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, message: null),
    };

    /// <summary>The outputs section's sentence for a recipe that declares none.</summary>
    internal const string NoOutputs = "This workflow doesn't produce anything.";

    /// <summary>The mark between two neighbouring states that loop into each other, with the loop's name (when it has one) and cap.</summary>
    /// <param name="name">The loop's name, or null.</param>
    /// <param name="cap">The round cap.</param>
    /// <returns>The mark and the cap, "⇄ rounds ≤2".</returns>
    internal static string LoopLink(string? name, int cap) => string.Create(CultureInfo.InvariantCulture, $"{LoopMark} {(string.IsNullOrEmpty(name) ? string.Empty : name + " ")}≤{cap}");

    /// <summary>What a loop link says in words, for a screen reader.</summary>
    /// <param name="name">The loop's name, or null.</param>
    /// <param name="cap">The round cap.</param>
    /// <returns>The words, "rounds, up to 2 rounds".</returns>
    internal static string LoopCapWords(string? name, int cap) => string.Create(CultureInfo.InvariantCulture, $"{(string.IsNullOrEmpty(name) ? string.Empty : name + ", ")}up to {cap} rounds");

    /// <summary>The note on a state whose loop goes to a state that is not its neighbour.</summary>
    /// <param name="name">The loop's name, or null.</param>
    /// <param name="to">The state it goes to.</param>
    /// <param name="cap">The round cap.</param>
    /// <returns>The sentence.</returns>
    internal static string LoopNote(string? name, string to, int cap) => $"loops to {to}: {LoopCapWords(name, cap)}";

    /// <summary>The note on a state that loops into itself.</summary>
    /// <param name="name">The loop's name, or null.</param>
    /// <param name="cap">The round cap.</param>
    /// <returns>The sentence.</returns>
    internal static string LoopNoteSelf(string? name, int cap) => $"loops on itself: {LoopCapWords(name, cap)}";

    /// <summary>An output under the state it comes from.</summary>
    /// <param name="label">The output's label, or its key.</param>
    /// <param name="type">The type word.</param>
    /// <returns>The line.</returns>
    internal static string OutputLine(string label, string type) => $"{label} ({type})";

    /// <summary>A fact type in the word the API serves it in.</summary>
    /// <param name="type">The type (a word this build doesn't know reads as payload).</param>
    /// <returns>The word.</returns>
    internal static string FactTypeWord(FactType type) => type switch
    {
        FactType.Text => "text",
        FactType.File => "file",
        FactType.Item => "item",
        FactType.Link => "link",
        FactType.ScopeMap => "scope-map",
        FactType.Packet => "packet",
        FactType.Evidence => "evidence",
        FactType.Decision => "decision",
        FactType.Payload => "payload",
        FactType.Findings => "findings",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, message: null),
    };

    /// <summary>An input's detail line.</summary>
    /// <param name="input">The input.</param>
    /// <returns>Key, type, kind and whether it is required.</returns>
    internal static string InputDetail(RecipeInputView input) => $"{input.Key} · {FactTypeWord(input.Type)} · {input.Kind} · {(input.Required ? "required" : "optional")}";

    /// <summary>An output's detail line.</summary>
    /// <param name="output">The output.</param>
    /// <returns>Key, type and the state it comes from.</returns>
    internal static string OutputDetail(RecipeOutputView output) => $"{output.Key} · {FactTypeWord(output.Type)} · from {output.From}";

    /// <summary>An input's generate template.</summary>
    /// <param name="template">The template as served.</param>
    /// <returns>The line.</returns>
    internal static string GenerateLine(string template) => $"generated: {template}";

    /// <summary>The inputs section's heading.</summary>
    internal const string InputsHeading = "What it needs";

    /// <summary>The outputs section's heading.</summary>
    internal const string OutputsHeading = "What it produces";

    /// <summary>The sheet's skeleton while the recipe is read.</summary>
    internal const string LoadingRecipe = "Reading the workflow…";

    /// <summary>The recipe's name template, labelled.</summary>
    /// <param name="template">The template as served.</param>
    /// <returns>The line.</returns>
    internal static string NameTemplateLine(string template) => $"name {template}";

    /// <summary>The recipe sheet's disclosure.</summary>
    internal const string RecipeDetails = "Details";

    /// <summary>The link from an instance frame or a Workflows group head to the recipe sheet.</summary>
    internal const string RecipeLink = "How it works ▸";

    /// <summary>The recipe link's accessible name: the visible word first, then which recipe (and version) it opens.</summary>
    /// <param name="subject">The recipe link's subject, "id" or "id@hash12".</param>
    /// <returns>The words.</returns>
    internal static string OpenRecipeName(string subject) => $"How it works: {subject}";

    /// <summary>What the recipe sheet says when the recipe's answer is in a shape this build can't read.</summary>
    internal const string RecipeUnreadable = "This app can't read this workflow's answer.";
}
