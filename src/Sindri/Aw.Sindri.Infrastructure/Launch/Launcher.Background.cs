using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class Launcher
{
    /// <summary>A background launch opens no viewer (it runs unattended; the viewer opens on takeover). It says so with the viewer that will open, so the ledger never shows 'none' beside a configured one.</summary>
    private async ValueTask<ViewerOutcome> Background(CancellationToken cancellationToken)
    {
        var facts = await parts.Viewer.Probe(cancellationToken).ConfigureAwait(false);
        var kind = string.IsNullOrEmpty(facts.Kind) ? null : facts.Kind;
        return ViewerOutcome.Skipped with
        {
            ViewerKind = kind,
            ViewerLabel = kind is null || string.Equals(kind, "print", StringComparison.Ordinal) ? null : facts.Label,
            Reason = kind is null ? "background: no viewer is configured" : "background: viewer opens on takeover",
        };
    }
}
