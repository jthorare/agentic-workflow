namespace Aw.Sindri.Core.Graph.Validation;

/// <summary>What the validator asks about names a document uses but does not define.</summary>
public interface IGraphWorld
{
    /// <summary>Is <paramref name="name"/> a repo of the profile?</summary>
    /// <param name="name">The repo's short name.</param>
    /// <returns>The answer.</returns>
    public GraphLookup Repo(string name);

    /// <summary>Is <paramref name="name"/> a connection?</summary>
    /// <param name="name">The connection's name.</param>
    /// <returns>The answer.</returns>
    public GraphLookup Connection(string name);

    /// <summary>Is <paramref name="name"/> a memory store?</summary>
    /// <param name="name">The memory's name.</param>
    /// <returns>The answer.</returns>
    public GraphLookup Memory(string name);
}
