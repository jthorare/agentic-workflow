using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Testing;

/// <summary>The profile's 'index.concepts' in memory, with a hash that changes with its content. Kept honest by <see cref="ProfileConceptWriterContract"/>.</summary>
public sealed class FakeProfileConceptWriter : IProfileConceptWriter
{
    private readonly List<ConceptSettings> _concepts = [];
    private readonly List<IReadOnlyList<ConceptEdit>> _writes = [];

    /// <summary>Gets the entries now in 'index.concepts', in order.</summary>
    public IReadOnlyList<ConceptSettings> Concepts => _concepts;

    /// <summary>Gets the edits of each write, in order.</summary>
    public IReadOnlyList<IReadOnlyList<ConceptEdit>> Writes => _writes;

    /// <summary>Gets or sets the profile's hash: a test sets it to play a profile that changed since the caller read it.</summary>
    public ProfileHash Hash { get; set; } = HashOf([]);

    /// <summary>Gets or sets an error Write returns instead of writing.</summary>
    public Error? Fails { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<ProfileHash>> Write(IReadOnlyList<ConceptEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edits);
        if (Fails is { } error)
        {
            return ValueTask.FromResult<Result<ProfileHash>>(error);
        }

        if (ifHash is { } expected && expected != Hash)
        {
            return ValueTask.FromResult<Result<ProfileHash>>(SindriErrors.ProfileHashMismatch.With("the profile changed since you read it (fix: read it again; the sheet keeps your changes)"));
        }

        foreach (var edit in edits)
        {
            var at = _concepts.FindIndex(c => c.Id == edit.Id);
            if (edit.Entry is null)
            {
                if (at >= 0)
                {
                    _concepts.RemoveAt(at);
                }
            }
            else if (at >= 0)
            {
                _concepts[at] = edit.Entry with { Id = edit.Id };
            }
            else
            {
                _concepts.Add(edit.Entry with { Id = edit.Id });
            }
        }

        _writes.Add(edits);
        Hash = HashOf(_concepts);
        return ValueTask.FromResult<Result<ProfileHash>>(Hash);
    }

    private static ProfileHash HashOf(List<ConceptSettings> concepts)
    {
        var text = string.Join('\n', concepts.Select(static c => string.Join('|', c.Id?.Value, c.Name, c.Summary, Join(c.Aliases), Join(c.Features), Join(c.Paths), Join(c.DependsOn), Join(c.Owns))));
        return ProfileHash.From(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text))));
    }

    private static string Join(IReadOnlyList<string>? values) => values is null ? "-" : string.Join(',', values);
}
