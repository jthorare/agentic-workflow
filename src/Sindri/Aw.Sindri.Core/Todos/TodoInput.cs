using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Todos;

/// <summary>What a todo's typed fields become before they reach an event (todo spec D3, §4.3): checked, then scrubbed whoever wrote them. The forge hosts a PR link may name are the approved profile's 'workflow.forge.hosts', else github.com.</summary>
/// <param name="scrubber">Takes secrets out of the text.</param>
/// <param name="approvals">The approved profile.</param>
public sealed class TodoInput(ITextScrubber scrubber, IApprovedProfile approvals)
{
    private static readonly string[] _defaultHosts = ["github.com"];

    /// <summary>The text, checked by <see cref="TodoText"/> and scrubbed.</summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The text, SND-TODO-001 or SND-TODO-007.</returns>
    public Result<string> Text(string text)
    {
        var checkedText = TodoText.Check(text);
        return checkedText.IsError ? checkedText.Error : scrubber.Scrub(checkedText.Value);
    }

    /// <summary>The typed links as deep links.</summary>
    /// <param name="typed">The typed links.</param>
    /// <param name="cancellationToken">Cancels the read of the profile.</param>
    /// <returns>The deep links, or SND-TODO-004.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Links(IReadOnlyList<string> typed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typed);
        if (typed.Count == 0)
        {
            return Results.Success<IReadOnlyList<string>>([]);
        }

        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var hosts = latest.Value?.Profile.Workflow?.Forge?.Hosts is { Count: > 0 } named ? [.. named] : _defaultHosts;
        return TodoLinks.ParseAll(typed, hosts);
    }

    /// <summary>The text and links an edit asks for, each checked when given.</summary>
    /// <param name="text">The new text, or null to leave it.</param>
    /// <param name="links">The new whole list of typed links, or null to leave them.</param>
    /// <param name="cancellationToken">Cancels the read of the profile.</param>
    /// <returns>Both (null for one left alone), or the first refusal.</returns>
    public async ValueTask<Result<(string? Text, IReadOnlyList<string>? Links)>> Edit(string? text, IReadOnlyList<string>? links, CancellationToken cancellationToken)
    {
        string? newText = null;
        if (text is not null)
        {
            var checkedText = Text(text);
            if (checkedText.IsError)
            {
                return checkedText.Error;
            }

            newText = checkedText.Value;
        }

        IReadOnlyList<string>? newLinks = null;
        if (links is not null)
        {
            var parsed = await Links(links, cancellationToken).ConfigureAwait(false);
            if (parsed.IsError)
            {
                return parsed.Error;
            }

            newLinks = parsed.Value;
        }

        return (newText, newLinks);
    }
}
