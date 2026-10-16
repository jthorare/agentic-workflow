using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

/// <summary>Wraps a subject id so the JSON tests can send a missing or null property.</summary>
public sealed record SubjectIdHolder(SubjectId Id);
