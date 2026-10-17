using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 2: the pinned deployment files, fetched from the pinned commit and verified against the lock. Every run asks the fetch, which hashes the files already there and
// downloads only what is missing or doesn't match, so a deleted or tampered file is restored (or refused when the download doesn't match its pin) and the stamp, the
// pins, decides whether the later steps rerun.
internal sealed class OnyxFetchStep(IDeployFiles files, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Fetch;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp([.. context.Pins.Files.Select(static f => f.Path + " " + f.Sha256)]);

    public async ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, CancellationToken cancellationToken)
    {
        var announced = false;
        void Announce()
        {
            if (!announced)
            {
                announced = true;
                progress.Line($"{Name}: running");
            }
        }

        var fetched = await files.Fetch(context.Pins, context.Paths.DeployDir, line =>
        {
            if (!line.StartsWith("present ", StringComparison.Ordinal))
            {
                Announce();   // a file that is already right is verified quietly; a download is work
            }

            progress.Line(line);
        }, cancellationToken).ConfigureAwait(false);
        if (fetched.IsError)
        {
            return fetched.Error;
        }

        var changed = fetched.Value.Downloaded > 0 || !string.Equals(recorded, stamp, StringComparison.Ordinal);
        if (changed)
        {
            Announce();
        }

        return changed ? OnyxStepOutcome.Ran : OnyxStepOutcome.Skipped;
    }
}
