using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A resource allocator granted the wait's resource (spec §3.4). A grant isn't a stored trigger: a refused one's resume waits again (§5.8).</summary>
/// <param name="Resource">The resource.</param>
/// <param name="Grant">The allocator's grant number, unique per resource.</param>
public sealed record ResourceWaitSource(ResourceKind Resource, long Grant) : WaitSource
{
    /// <inheritdoc/>
    public override string KeyPart => string.Create(CultureInfo.InvariantCulture, $"resource:{(Resource == ResourceKind.Heavy ? "heavy" : "slot")}:{Grant}");
}
