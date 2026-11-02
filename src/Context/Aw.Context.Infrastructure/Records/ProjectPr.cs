using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Records;

/// <summary>One pull request a project reaches, as <see cref="RecordSql.ProjectPrs"/> reads it.</summary>
/// <param name="Project">The project's key.</param>
/// <param name="Pr">The pull request's key ('github:pr/&lt;owner&gt;/&lt;name&gt;/&lt;n&gt;').</param>
/// <param name="State">Its state.</param>
/// <param name="UpdatedAt">When it last changed; the sync time when the source gave none.</param>
internal sealed record ProjectPr(RecordKey Project, RecordKey Pr, RecordState? State, DateTimeOffset UpdatedAt);
