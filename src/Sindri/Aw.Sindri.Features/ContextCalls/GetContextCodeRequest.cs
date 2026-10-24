using Aw.Context.Core.Code;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The code lens around some anchors (GET /sindri/context/code?anchor=&amp;anchor=): files, tests, data things and schema files.</summary>
/// <param name="Anchors">Node keys, names or aliases.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version, or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextCodeRequest(IReadOnlyList<string> Anchors, bool Draft = false, string? MapVersion = null) : IQuery<CodeView>;
