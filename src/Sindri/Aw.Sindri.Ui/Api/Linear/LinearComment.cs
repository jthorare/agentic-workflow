namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A comment on an issue.</summary>
/// <param name="Id">The comment's Linear id.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="Body">The text.</param>
/// <param name="Sindri">True when Sindri wrote it.</param>
internal sealed record LinearComment(string Id, LinearPerson Author, DateTimeOffset CreatedAt, string Body, bool Sindri);
