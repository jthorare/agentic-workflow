using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>What the catalog needs to build the library feed: the machine's folders (built from the environment at the host edge, never read in Core), where passes leave their status, and the clock.</summary>
/// <param name="Host">The home folders the library's path rules are checked against.</param>
/// <param name="Status">Where the feed leaves what each pass found.</param>
/// <param name="Clock">The clock the status is stamped with.</param>
internal sealed record LibraryParts(LibraryHost Host, ILibraryStatus Status, TimeProvider Clock);
