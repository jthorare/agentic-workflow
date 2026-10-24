using Aw.Evidence.Core.Ids;

namespace Aw.Evidence.Core.Browser;

/// <summary>The <paramref name="Index"/>th (0-based, in DOM order) of the elements sharing <paramref name="TestId"/>.</summary>
/// <param name="TestId">The data-testid.</param>
/// <param name="Index">Which match, from 0.</param>
public sealed record NthTestIdLocator(TestId TestId, int Index) : Locator;
