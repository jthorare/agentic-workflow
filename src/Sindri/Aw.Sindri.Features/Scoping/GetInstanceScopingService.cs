using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The server-side read model of a scoping instance (scoping spec §8.4, API2): the instance's declared facts, read as the fact route reads
/// them (scrubbed), go through the same builder as 'scoping show --facts', so the view is live before evidence, each section 'not yet' with the
/// state that writes it. The Host fences the text for a session caller.
/// </summary>
/// <param name="instances">Opens the instance for the caller.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="scrubber">Removes secrets from the text.</param>
internal sealed class GetInstanceScopingService(ScopingInstances instances, IFactContents contents, ITextScrubber scrubber)
    : IService<GetInstanceScopingRequest, ScopingView>
{
    public async ValueTask<Result<ScopingView>> Handle(GetInstanceScopingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var view = await instances.Open(request.Instance, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var facts = await Facts(view.Value, cancellationToken).ConfigureAwait(false);
        return facts.IsError ? facts.Error : ScopingViewBuilder.Build(new ScopeFacts(facts.Value));
    }

    private async ValueTask<Result<Dictionary<string, string>>> Facts(InstanceView view, CancellationToken cancellationToken)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in ScopingFactKeys.All)
        {
            var text = await ScopingFactText.Read(view, key, contents, cancellationToken).ConfigureAwait(false);
            if (text.IsError)
            {
                return text.Error;
            }

            if (text.Value is { } body)
            {
                facts[key] = scrubber.Scrub(body);
            }
        }

        return facts;
    }
}
