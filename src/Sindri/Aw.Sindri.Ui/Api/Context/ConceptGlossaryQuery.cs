namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What GET /sindri/context/concepts/glossary is asked: a repo and the map version the concepts were read at.</summary>
/// <param name="Repo">The repo to read; empty for the profile's only repo.</param>
/// <param name="MapVersion">The map version to read at; null for the current one.</param>
internal sealed record ConceptGlossaryQuery(string Repo, string? MapVersion = null);
