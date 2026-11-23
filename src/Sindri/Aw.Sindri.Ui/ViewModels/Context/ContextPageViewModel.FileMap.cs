using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map seam (U9d owns this file).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The file map.</summary>
    public FileMapViewModel FileMap { get; private set; }

    /// <summary>The code lens' callers and callees panels (S-9).</summary>
    internal CodeLensViewModel Code { get; } = new();

    [MemberNotNull(nameof(FileMap))]
    private void InitFileMap() => FileMap = new FileMapViewModel(Ports) { OpenFile = OpenFileMap };

    // Enter on a file row: the map around that file, as typing its key and asking would draw it (#3677).
    private Task OpenFileMap(string path)
    {
        ShowFiles = false;
        return Pick("code:" + path);
    }

    /// <summary>Reads the code lens for some anchors into <see cref="Code"/>: the callers and callees the code index gave. A refusal or an unreachable API leaves the panels as they were and says so in the result.</summary>
    /// <param name="anchors">One to 20 anchor keys.</param>
    /// <param name="draft">Read the draft map.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the panels now show this answer.</returns>
    internal Task<bool> ReadCode(IReadOnlyList<string> anchors, bool draft, CancellationToken cancellationToken) =>
        ReadCode(Code.Begin(), anchors, draft, cancellationToken);

    // A read that is no longer the latest (a newer selection, Esc) draws nothing.
    private async Task<bool> ReadCode(int generation, IReadOnlyList<string> anchors, bool draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        if (Ports.ApiUrl() is not { } api)
        {
            return false;
        }

        var result = await Ports.Api.Code(api, new CodeQuery(anchors, draft, MapVersion: null), cancellationToken).ConfigureAwait(true);
        if (!result.IsSuccess || !Code.IsCurrent(generation))
        {
            return false;
        }

        Code.Show(result.Value.Callers, result.Value.Callees);
        return true;
    }
}
