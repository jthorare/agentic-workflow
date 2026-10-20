using System.Text;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// The server-side read model of a workflow.plan instance (line spec LN-API2): the instance's facts, read as the fact route reads them (scrubbed), go through the same builder
/// as 'plan show --facts', so the view is live before evidence, each section 'not yet' until its facts are there. The Host fences the text for a session caller.
/// </summary>
/// <param name="instances">Opens the instance for the caller.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
internal sealed class GetInstanceDesignStopService(PlanInstances instances, IFactContents contents, ITextScrubber scrubber)
    : IService<GetInstanceDesignStopRequest, DesignStopView>
{
    private static readonly string[] _keys = ["design", "plan", "holdoutSets", "freezeRefused", "lensRounds"];

    public async ValueTask<Result<DesignStopView>> Handle(GetInstanceDesignStopRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var view = await instances.Open(request.Instance, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in _keys)
        {
            if (view.Value.FactShas?.GetValueOrDefault("fact:" + key) is not { } sha)
            {
                continue;
            }

            var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            facts[key] = scrubber.Scrub(Encoding.UTF8.GetString(bytes.Value.Span));
        }

        return DesignStopViewBuilder.Build(new LineFacts(DesignStopFacts.Files(facts)));
    }
}
