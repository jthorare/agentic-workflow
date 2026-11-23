using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.ScanRepos;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/setup/repos?root=~&amp;depth=3 (first-run spec §3.3, A3): the repo scan as NDJSON, one line per main checkout as the walk finds it, then '{done, folders, repos, knownFailed}'.
/// A refusal (SND-AUTH-002, SND-SETUP-008, a bad depth) is a problem answer before any line. The POST on the same path is AddRepo.
/// </summary>
internal static class ScanReposEndpoint
{
    internal static RouteGroupBuilder MapScanRepos(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/setup/repos", static async (string? root, string? depth, IService<ScanReposRequest, ScanReposResponse> service, JsonSerializerOptions json, CancellationToken ct) =>
        {
            int? levels = null;
            if (depth is not null)
            {
                if (!int.TryParse(depth, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                {
                    return HttpResults.Problem(SindriErrors.InvalidInput.With($"depth must be a whole number, not '{depth}' (fix: depth=3)"));
                }

                levels = parsed;
            }

            var scan = await service.Handle(new ScanReposRequest(root, levels), ct).ConfigureAwait(false);
            return scan.IsSuccess ? new RepoScanResult(scan.Value, json) : HttpResults.Problem(scan.Error);
        });
        return group;
    }
}
