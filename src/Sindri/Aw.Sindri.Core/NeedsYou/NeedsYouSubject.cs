using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What a needs-you row is about: '{kind, id}'. (Not Workflow.Views.SubjectRef, which names an instance's subject.)</summary>
/// <param name="Kind">The kind of thing.</param>
/// <param name="Id">Its id as text: 'DEC-…', a service name, the live profile hash.</param>
public sealed record NeedsYouSubject(SubjectKind Kind, SubjectId Id);
