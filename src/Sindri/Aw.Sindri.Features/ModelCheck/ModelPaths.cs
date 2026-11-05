using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.ModelCheck;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// The checks that read the workspace's paths and the packet in force, before any TLC call (model-check design §3, §4.1): a README's 'models:' path that no longer exists is an MDL-SPEC-002 warning, and a
/// scoping model the build didn't move into specs/tla/ (or moved with a README that models nothing real) is MDL-SPEC-003.
/// </summary>
/// <param name="folders">Asks whether a path exists.</param>
/// <param name="packets">Reads the packet in force.</param>
internal sealed class ModelPaths(ISpecFolders folders, IPacketDirectory packets)
{
    private const string MoveRule = "MDL-SPEC-003";
    private const string StaleRule = "MDL-SPEC-002";
    private const string Moved = "specs/tla/";

    /// <summary>MDL-SPEC-003: the scoping models in the packet in force that the head doesn't carry as specs/tla/&lt;Name&gt;/ with a README that models something real.</summary>
    /// <param name="context">The step's context, for fact:activePacket.</param>
    /// <param name="workspace">The head's worktree.</param>
    /// <param name="all">Every spec folder the glob found.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The findings; none when the instance holds no packet or it holds no model; an error when the packet can't be read.</returns>
    internal async ValueTask<Result<List<RuleFinding>>> Move(StepContext context, string workspace, List<Spec> all, CancellationToken cancellationToken)
    {
        if (StepFacts.Field(StepFacts.Read(context, "fact:activePacket"), "dir") is not { } dir)
        {
            return new List<RuleFinding>();
        }

        var read = await packets.Read(dir, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var findings = new List<RuleFinding>();
        foreach (var name in read.Value.Keys.Where(static k => k.StartsWith("model/", StringComparison.Ordinal) && k.IndexOf('/', 6) > 6).Select(static k => k[6..k.IndexOf('/', 6)]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var spec = all.Find(s => string.Equals(s.Folder.Path[(s.Folder.Path.LastIndexOf('/') + 1)..], name, StringComparison.Ordinal));
            if (spec is null)
            {
                findings.Add(new RuleFinding(MoveRule, $"The scoping model {name} was not moved: the head has no {Moved}{name}/ (fix: move model/{name}/ of the packet to {Moved}{name}/ in this change)", $"{Moved}{name}/"));
            }
            else if (!await AnyExists(workspace, spec.Readme.Models ?? [], cancellationToken).ConfigureAwait(false))
            {
                findings.Add(new RuleFinding(MoveRule, $"The moved model {name} models nothing that exists: its README's 'models:' line names no path of the head (fix: put the real files the model describes on line 1 of {spec.Folder.Path}/README.md)", $"{spec.Folder.Path}/README.md"));
            }
        }

        return findings;
    }

    /// <summary>MDL-SPEC-002: a 'models:' path that doesn't exist, one warning per path.</summary>
    /// <param name="workspace">The head's worktree.</param>
    /// <param name="specs">The specs that run.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>The warnings.</returns>
    internal async ValueTask<List<RuleFinding>> Stale(string workspace, List<Spec> specs, CancellationToken cancellationToken)
    {
        var warnings = new List<RuleFinding>();
        foreach (var spec in specs)
        {
            foreach (var path in spec.Readme.Models ?? [])
            {
                if (!await folders.Exists(workspace, path, cancellationToken).ConfigureAwait(false))
                {
                    warnings.Add(new RuleFinding(StaleRule, $"{spec.Folder.Path}/README.md models {path}, which does not exist (fix: update the 'models:' line, or the spec no longer follows its code)", $"{spec.Folder.Path}/README.md"));
                }
            }
        }

        return warnings;
    }

    private async ValueTask<bool> AnyExists(string workspace, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            if (await folders.Exists(workspace, path, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
