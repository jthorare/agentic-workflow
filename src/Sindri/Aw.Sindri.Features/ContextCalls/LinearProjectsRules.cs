using System.Globalization;
using Aw.Context.Core.Projects;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The words of 'context:index-linear-projects' (projects page spec P2, §4.2): how many projects the 'linear-projects' feed stored, how many the key's owner leads,
/// and whose Linear key it is, so a shared or bot key shows here as someone else's name before the Projects page says "leads nothing".
/// </summary>
internal static class LinearProjectsRules
{
    /// <summary>Ok with the counts and the key's owner; warn when the stored projects can't be read or the owner isn't read yet; ok before the first read ends.</summary>
    /// <param name="facts">The stored projects, as the Projects page reads them.</param>
    /// <returns>The row.</returns>
    internal static DoctorCheck Judge(Result<ProjectFactsAnswer> facts)
    {
        if (facts.IsError)
        {
            return new DoctorCheck(IndexDoctorChecks.LinearProjectsName, CheckState.Warn, $"the stored projects can't be read: {facts.Error.Code} {facts.Error.Message}", "sindri index status");
        }

        var answer = facts.Value;
        if (!answer.FirstReadDone)
        {
            return new DoctorCheck(IndexDoctorChecks.LinearProjectsName, CheckState.Ok, "the first read of Linear's projects isn't done yet (see context:index-sync)", Fix: null);
        }

        var stored = answer.Catalog.Count == 1 ? "1 project stored" : string.Create(CultureInfo.InvariantCulture, $"{answer.Catalog.Count} projects stored");
        return answer.Owner is { } owner
            ? new DoctorCheck(IndexDoctorChecks.LinearProjectsName, CheckState.Ok,
                string.Create(CultureInfo.InvariantCulture, $"{stored}, {answer.Catalog.Count(static p => p.LeadIsOwner)} led by {owner.Name ?? owner.Id.Value} (the Linear key's owner)"), Fix: null)
            : new DoctorCheck(IndexDoctorChecks.LinearProjectsName, CheckState.Warn, $"{stored}, but whose Linear key this is isn't read yet, so the Projects page can't pick out the ones you lead", "sindri index sync linear-projects");
    }
}
