using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.CreateToolkit;

/// <summary>
/// Makes a toolkit (see <see cref="CreateToolkitRequest"/>): checks the name, refuses one that has a row, runs the generator and writes the row in state 'off'. The generator takes its own heavy-job lock, so this
/// job takes none. A name the generator couldn't turn back into the same folder name (it makes PascalCase names from words) is refused before it runs.
/// </summary>
/// <param name="store">The toolkit store.</param>
/// <param name="folders">The toolkits root and its generator.</param>
/// <param name="jobs">The running job.</param>
/// <param name="callers">Who asked; the row records the caller's actor as its creator.</param>
internal sealed partial class CreateToolkitService(IToolkitStore store, IToolkitFolders folders, IJobContextAccessor jobs, ICallerContextAccessor callers) : IService<CreateToolkitRequest, CreateToolkitResult>
{
    public async ValueTask<Result<CreateToolkitResult>> Handle(CreateToolkitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Refusal(request) is { } refused)
        {
            return refused;
        }

        var found = await store.Find(request.Name, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not null)
        {
            return SindriErrors.ToolkitExists.With($"'{request.Name}' (fix: pick another name; sindri toolkit list shows the toolkits)");
        }

        jobs.Current?.Progress.Report("generating the toolkit project (restore, build and its first test)");
        var made = await folders.Generate(request.Name, request.Tool, request.Needs, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var toolkit = new Toolkit(request.Name, made.Value.Dir, ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, callers.Current?.Actor ?? "owner");
        var saved = await store.Save(toolkit, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new CreateToolkitResult(toolkit.Name, toolkit.Dir, toolkit.State);
    }

    private static Error? Refusal(CreateToolkitRequest request)
    {
        if (request.Name is null || !Words.IsMatch(request.Name) || !ToolNames.IsToolkitName(request.Name))
        {
            return SindriErrors.ToolkitNameInvalid.With("A new toolkit's name is lowercase words joined by '-', each starting with a letter (github-triage).");
        }

        var named = ToolNames.For(request.Name, request.Tool);
        return named.IsError ? named.Error : null;
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z][a-z0-9]*)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words { get; }
}
