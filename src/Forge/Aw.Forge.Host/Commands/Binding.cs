using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>Shared binding for the commands: the port from the container and a link or repo parsed through it.</summary>
internal static class Binding
{
    internal static IForge Forge(IServiceProvider services) => services.GetRequiredService<IForge>();

    internal static Result<ForgeRepo> Repo(IForge forge, string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
        {
            return ForgeErrors.Invalid.With("repo: expected host/owner/name");
        }

        return forge.Parse($"https://{parts[0]}/{parts[1]}/{parts[2]}/pull/1").Map(r => r.Repository);
    }

    internal static Result<CommentKind> Kind(string? value) =>
        value is null ? CommentKind.Comment
        : Enum.TryParse<CommentKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind
        : ForgeErrors.Invalid.With("kind: thread, review or comment");
}
