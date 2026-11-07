using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The project names the Reviews list's sections carry, set by the test.</summary>
public sealed class FakeReviewProjects : IReviewProjects
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);

    /// <summary>Names a project.</summary>
    /// <param name="key">The key a <c language="text">review.projects[].project</c> entry holds.</param>
    /// <param name="name">The project's name.</param>
    /// <returns>This fake.</returns>
    public FakeReviewProjects With(string key, string name)
    {
        _names[key] = name;
        return this;
    }

    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Names(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_names, StringComparer.Ordinal)));
}
