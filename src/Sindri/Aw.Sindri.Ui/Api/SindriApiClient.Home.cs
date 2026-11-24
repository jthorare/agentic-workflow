using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Home read (<see cref="SindriApiClient"/>, continued; home spec §8).</summary>
internal sealed partial class SindriApiClient
{
    /// <summary>The path of GET /sindri/home.</summary>
    internal const string HomePath = "/sindri/home";

    /// <inheritdoc />
    public Task<Result<HomeView>> Home(Uri api, string? after, CancellationToken cancellationToken) =>
        Get<HomeView>(api, after is null ? HomePath : HomePath + "?after=" + Uri.EscapeDataString(after), cancellationToken, view: true);
}
