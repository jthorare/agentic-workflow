using System.Text.Json.Nodes;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.intake' on the block runtime (babysitter spec §4.2): reads the pull request once through the forge port, which refuses a host outside the profile's list
/// before it sends anything, then outputs the workspace it was given (empty when none) and seeds the empty facts every later Step reads, so each finds them on every path.
/// A pull request it can't read fails the Step. The hand-off hash check and cutting a workspace from the PR head are not done here: no port reaches them yet.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
internal sealed class PrIntakeStep(IPullRequests pulls) : IBlockStep, IBlockStepId
{
    private static readonly string[] _seeds = ["fact:decision", "fact:ownerNote", "fact:handoffNote", "fact:fix", "fact:reply", "fact:recheck"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.intake");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrLink.Of(context) is not { } link)
        {
            return BlockStepResult.Failed("step.pr.intake needs the pull request's link (fact:pr), which the instance does not hold");
        }

        var pull = await pulls.Get(link, cancellationToken).ConfigureAwait(false);
        if (pull.IsError)
        {
            return BlockStepResult.Failed($"step.pr.intake can't read {link}: {pull.Error.Message}");
        }

        var outputs = new List<(string Key, JsonNode Value)> { ("fact:workspace", JsonValue.Create(StepFacts.Text(context, "fact:workspace") ?? string.Empty)) };
        outputs.AddRange(_seeds.Select(static key => (key, (JsonNode)new JsonObject())));
        return StepFacts.Done("seeded", [.. outputs]);
    }
}
