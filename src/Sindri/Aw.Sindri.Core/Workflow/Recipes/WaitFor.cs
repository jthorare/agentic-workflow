using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// What a wait state waits for (spec §3.4): exactly one of an event, an answer, a resource or a child. One record
/// with four optional fields, so the long form binds without a union converter (W3); the schema rules check that
/// exactly one is set.
/// </summary>
/// <param name="Event">An event of this kind (a registered EventDescriptor).</param>
/// <param name="Answer">A human's answer, recorded as 'fact:answer'.</param>
/// <param name="Resource">A heavy-job slot or a session slot.</param>
/// <param name="Child">A child instance of another recipe.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WaitFor(EventKind? Event, AnswerWait? Answer, ResourceKind? Resource, ChildWait? Child);
