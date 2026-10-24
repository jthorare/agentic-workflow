using System.Text.Json;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Desktop;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Features.DesktopRun;

/// <summary>Reads the repo's evidence config and the screens manifest and turns them into the run's plan; any problem with either is a refusal.</summary>
internal sealed class DesktopPlans(IEvidenceTargets targets, JsonSerializerOptions json)
{
    internal async ValueTask<Result<DesktopPlan>> PlanAsync(DesktopRunRequest request, byte[] manifestBytes, CancellationToken cancellationToken)
    {
        var loaded = await targets.LoadAsync(request.Repo, request.ConfigPath, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        if (loaded.Value.Desktop is not { } settings)
        {
            return EvidenceErrors.TargetsInvalid.With("the evidence config has no 'desktop' part (.agentic-workflow/evidence.json)");
        }

        var manifest = DesktopManifestReader.Read(manifestBytes, json);
        if (manifest.IsError)
        {
            return manifest.Error with { Message = $"manifest {request.ManifestPath}: {manifest.Error.Message}" };
        }

        var screens = Chosen(manifest.Value, request.Screens);
        if (screens.IsError)
        {
            return screens.Error;
        }

        var design = manifest.Value.DesignRoot == "." ? Folder(request.ManifestPath) : Normalize(RunPath.Combine(Folder(request.ManifestPath), manifest.Value.DesignRoot));
        return new DesktopPlan(request.Repo, request.RunDirectory, design, settings, manifest.Value.Threshold ?? settings.Threshold, screens.Value);
    }

    private static Result<IReadOnlyList<DesktopScreen>> Chosen(DesktopManifest manifest, IReadOnlyList<string>? names)
    {
        if (names is not { Count: > 0 })
        {
            return Results.Success(manifest.Screens);
        }

        var unknown = names.Where(n => !manifest.Screens.Any(s => string.Equals(s.Name, n, StringComparison.Ordinal))).ToList();
        return unknown.Count > 0
            ? EvidenceErrors.Invalid.With($"--screens names no screen of the manifest: {string.Join(", ", unknown)}")
            : Results.Success<IReadOnlyList<DesktopScreen>>([.. manifest.Screens.Where(s => names.Contains(s.Name, StringComparer.Ordinal))]);
    }

    // '..' folded in, so the served folder is a plain path (the manifest sits below the repo, its mockups may sit elsewhere in it).
    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".." && parts.Count > 0)
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else if (segment is not ("." or ".."))
            {
                parts.Add(segment);
            }
        }

        return "/" + string.Join('/', parts);
    }

    private static string Folder(string path) => path[..Math.Max(1, path.LastIndexOf('/'))];
}
