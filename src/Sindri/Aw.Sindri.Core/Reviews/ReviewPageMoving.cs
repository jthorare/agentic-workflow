namespace Aw.Sindri.Core.Reviews;

/// <summary>The head moved twice during the read.</summary>
/// <param name="Text">The words: The PR is moving; reload when it settles.</param>
public sealed record ReviewPageMoving(
    string Text);
