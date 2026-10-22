using System.Text.Json.Nodes;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Docs;

/// <summary>
/// 'step.docs.check' on the block runtime (docs-sync spec §7.2, API17): the docs commit holds doc paths only (DOCS-PATH-001: the profile's globs, or a doc the design
/// names), its relative links resolve (DOCS-LINK-001) and its markdown parses (DOCS-MD-001: every code fence closed). A worktree with no change fails
/// (DOCS-CHANGE-001), because there would be no docs PR to open. 'fact:docsScope' is { base, paths (the globs), candidates: [{ path, designNamed }] }.
/// </summary>
/// <param name="diff">Lists the paths the docs worktree changed since the scope's base.</param>
/// <param name="files">Reads the changed docs and the files their links name.</param>
internal sealed class DocsCheckStep(IRunDiff diff, IBriefFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.docs.check");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var workspace = StepFacts.Text(context, "fact:workspace");
        if (workspace is null || StepFacts.Read(context, "fact:docsScope") is not JsonObject scope || (string?)scope["base"] is null)
        {
            return BlockStepResult.Failed("step.docs.check needs fact:docsScope (with its base) and fact:workspace (a path)");
        }

        var checkedDocs = await new DocsCheckService(diff, files).Handle(new DocsCheckRequest(workspace, scope), cancellationToken).ConfigureAwait(false);
        if (checkedDocs.IsError)
        {
            return BlockStepResult.Failed($"step.docs.check can't read {workspace}: {checkedDocs.Error.Message}");
        }

        var findings = checkedDocs.Value.Findings.Select(static f => DocsFinding.Of(f.Rule, f.Item, f.Message, f.Fix)).ToList();
        return findings.Count == 0 ? StepFacts.Done("pass") : StepFacts.Done("fail", ("fact:findings", new JsonArray([.. findings])));
    }
}
