using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>An open that passed its checks (<see cref="OpenChecks"/>).</summary>
/// <param name="Item">The item, as the views hold it.</param>
/// <param name="Session">The launch whose session an attach or resume addresses, or null for an open.</param>
internal sealed record OpenAdmission(ItemRow Item, LaunchRow? Session);
