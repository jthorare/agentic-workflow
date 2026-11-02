using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'launch.waiting' payload (sandbox spec §6, SB16 (3)): an engine launch found the session cap full and waits for a free slot. Key 'launch.waiting:&lt;launchId&gt;'; the first wait is the one recorded, and 'launch.outcome' ends it.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Running">How many sessions were running.</param>
/// <param name="Cap">The most sessions that may run (the profile's 'sandbox.limits.sessions').</param>
/// <param name="Instance">The workflow instance the launch belongs to, or null.</param>
/// <param name="State">The Step's state id in that instance, or null.</param>
/// <param name="Attempt">The Step run's number, or null.</param>
public sealed record LaunchWaiting([property: JsonPropertyName("launchId")] LaunchId Launch, ItemId Item, int Running, int Cap,
    InstanceAddress? Instance = null, string? State = null, int? Attempt = null);
