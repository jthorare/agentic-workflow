using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>
/// What the Context page and its stage view models read through, passed once so a later stage takes what it needs without
/// editing the shell or the page's constructor.
/// </summary>
/// <param name="Api">Sindri's API.</param>
/// <param name="ApiUrl">The control plane's API address now, or null when none is known.</param>
/// <param name="Clipboard">Where Copy puts a CLI.</param>
/// <param name="Clock">The clock for every timing rule.</param>
/// <param name="Catalog">The command catalog, for the desk rows and the lock buttons' CLI.</param>
/// <param name="Follow">Registers a view to re-read on connect and after every event (the shell's Health.Follow), so a stage registers its own refresh from its own view model.</param>
/// <param name="Launcher">Hands a link to the system; null when the host has none.</param>
/// <param name="Pages">Reads and writes the owner's pages for the page sheet (pages spec §3.6); null until the host wires it.</param>
/// <param name="Writes">The write-then-approve flow every profile sheet shares (the page sheet's and the pin sheet's); null until the host wires it.</param>
/// <param name="OpenPin">Opens the Pin sheet for what the concept layer hands it; null until the host wires it (the layer then has no pin button).</param>
internal sealed record ContextPorts(ISindriApi Api, Func<Uri?> ApiUrl, IClipboard Clipboard, TimeProvider Clock, CatalogViewModel Catalog, Action<Func<Uri, CancellationToken, Task>> Follow, ILauncher? Launcher = null, Whole.IPageWrites? Pages = null, ProfileWriteFlow? Writes = null, Action<PinSheetOpen>? OpenPin = null);
