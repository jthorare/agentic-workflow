namespace Aw.Sindri.Core.Scheduling;

/// <summary>What the build's two profile-or-diagnostic services do, as GET /sindri/services says it. The services live in their own slices; the list reads the words from here.</summary>
public static class ServiceDescriptions
{
    /// <summary>The heartbeat service's name.</summary>
    public const string HeartbeatName = "heartbeat";

    /// <summary>What the heartbeat service does.</summary>
    public const string Heartbeat = "Records that Sindri is alive.";

    /// <summary>The job-probe service's name.</summary>
    public const string JobProbeName = "job-probe";

    /// <summary>What the job-probe service does.</summary>
    public const string JobProbe = "Runs a short job so the job panel can be tried.";

    /// <summary>The sentence for a service the build names here.</summary>
    /// <param name="name">The service name.</param>
    /// <returns>Its sentence, or null when this type does not know the service.</returns>
    public static string? Of(string name) => name switch
    {
        HeartbeatName => Heartbeat,
        JobProbeName => JobProbe,
        _ => null,
    };
}
