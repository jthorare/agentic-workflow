using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>The step's 'with': <c language="text">specs</c> (a folder glob, default 'specs/tla/*/'), <c language="text">scope</c> ('all' or 'touched') and <c language="text">witnesses</c> (default true).</summary>
/// <param name="Specs">The folder glob, relative to the workspace.</param>
/// <param name="Touched">True for 'scope: touched'.</param>
/// <param name="Witnesses">Whether the gaps/ cfgs run too.</param>
internal sealed record ModelCheckOptions(string Specs, bool Touched, bool Witnesses)
{
    /// <summary>The default folder glob.</summary>
    internal const string DefaultSpecs = "specs/tla/*/";

    /// <summary>Reads the 'with' of the step's context.</summary>
    /// <param name="context">The step's context.</param>
    /// <returns>The options; anything absent or of the wrong type takes its default.</returns>
    internal static ModelCheckOptions From(StepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var specs = context.Arguments.TryGetValue("specs", out var s) && s.ValueKind == JsonValueKind.String && s.GetString() is { } text && !string.IsNullOrWhiteSpace(text) ? text : DefaultSpecs;
        var touched = context.Arguments.TryGetValue("scope", out var scope) && scope.ValueKind == JsonValueKind.String && string.Equals(scope.GetString(), "touched", StringComparison.Ordinal);
        var witnesses = !context.Arguments.TryGetValue("witnesses", out var w) || w.ValueKind != JsonValueKind.False;
        return new ModelCheckOptions(specs, touched, witnesses);
    }
}
