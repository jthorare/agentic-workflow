using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Features.Leftovers;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri onyx down --purge': removes what an old `sindri onyx up` left on the box (owned-index spec §8.2 step 5): the containers and volumes labelled with its compose project, the keychain entry Sindri recorded and the onyx/ directory. Lists them, asks the human, then deletes in that order.
/// The human's verb (a session is refused SND-AUTH-002), local, no control plane. Exit 0 done, declined or nothing left, 1 a step failed (SND-INDEX-014 names the fix), 2 refused.
/// Self-contained on purpose: it reads onyx/state.json with its own record and uses nothing from the Onyx setup code, so deleting that code leaves this building.
/// </summary>
/// <param name="home">The Sindri home (AW_SINDRI_HOME).</param>
/// <param name="docker">Lists and removes by compose label.</param>
/// <param name="keychain">Deletes one keychain entry.</param>
/// <param name="output">Where the list and answers go.</param>
/// <param name="input">Where the human's answer is read.</param>
/// <param name="question">Where the question is asked (standard error).</param>
internal sealed class OnyxPurgeCommand(string home, IPurgeDocker docker, IPurgeKeychain keychain, Output output, TextReader input, TextWriter question)
{
    internal async Task<int> Run(bool human, CancellationToken ct)
    {
        if (!human)
        {
            return await output.Error(SindriErrors.AuthRefused.With("'onyx down --purge' deletes data and is the human's verb (fix: ask the human to run it at a terminal)")).ConfigureAwait(false);
        }

        var plan = OnyxLeftovers.Find(home, docker);
        if (plan.IsError)
        {
            return await Failed(plan.Error).ConfigureAwait(false);
        }

        if (plan.Value.IsEmpty)
        {
            return await output.Text("Nothing left from Onyx on this box.\n", 0).ConfigureAwait(false);
        }

        _ = await output.Text(Describe(plan.Value), 0).ConfigureAwait(false);
        await question.WriteAsync("Delete all of it? This cannot be undone. [y/N] ").ConfigureAwait(false);
        var answer = await input.ReadLineAsync(ct).ConfigureAwait(false);
        if (answer is null || (!answer.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) && !answer.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)))
        {
            return await output.Text("Nothing deleted.\n", 0).ConfigureAwait(false);
        }

        var done = Delete(plan.Value);
        return done.IsError ? await Failed(done.Error).ConfigureAwait(false) : await output.Text("Onyx's leftovers are gone.\n", 0).ConfigureAwait(false);
    }

    internal static OnyxPurgeCommand Create(IServiceProvider services, Output output, TextReader input, TextWriter question)
        => new(services.GetRequiredService<ISindriHome>().Path, services.GetRequiredService<IPurgeDocker>(), services.GetRequiredService<IPurgeKeychain>(), output, input, question);

    private static string Describe(PurgePlan plan)
    {
        var text = new StringBuilder("This deletes what an old `sindri onyx up` left on this box:\n");
        foreach (var name in plan.Containers)
        {
            _ = text.Append("  container ").AppendLine(name);
        }

        foreach (var name in plan.Volumes)
        {
            _ = text.Append("  volume    ").AppendLine(name);
        }

        if (plan.Keychain is not null)
        {
            _ = text.Append("  keychain  keychain:").AppendLine(plan.Keychain);
        }

        if (plan.Directory is not null)
        {
            _ = text.Append("  directory ").AppendLine(plan.Directory);
        }

        return text.ToString();
    }

    private async Task<int> Failed(Error error)
    {
        _ = await output.Error(error).ConfigureAwait(false);
        return error.Code == SindriErrors.AuthRefused.Code ? 2 : 1;
    }

    private Result<Unit> Delete(PurgePlan plan)
    {
        if (plan.Containers.Count > 0 && docker.RemoveContainers(plan.Containers) is { IsError: true } containers)
        {
            return containers.Error;
        }

        if (plan.Volumes.Count > 0 && docker.RemoveVolumes(plan.Volumes) is { IsError: true } volumes)
        {
            return volumes.Error;
        }

        if (plan.Keychain is not null)
        {
            var parts = plan.Keychain.Split('/');
            if (keychain.Remove(parts[0], parts[1]) is { IsError: true } removed)
            {
                return removed.Error;
            }
        }

        try
        {
            if (plan.Directory is not null)
            {
                Directory.Delete(plan.Directory, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.IndexOnyxLeftover.With($"{plan.Directory} could not be deleted ({e.GetType().Name}) (fix: remove it by hand, then run `sindri onyx down --purge` again)");
        }

        return Aw.Kernel.Results.Ok();
    }
}
