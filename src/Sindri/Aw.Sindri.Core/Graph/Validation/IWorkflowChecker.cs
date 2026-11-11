namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>Builds the validator with everything it needs from the running Sindri: the block catalog, the repos of the profile, the cron and condition parsers.</summary>
public interface IWorkflowChecker
{
    /// <summary>The validator for this moment's profile and blocks.</summary>
    /// <param name="cancellationToken">Cancels the read of the profile.</param>
    /// <returns>The validator.</returns>
    public ValueTask<GraphValidator> Validator(CancellationToken cancellationToken);
}
