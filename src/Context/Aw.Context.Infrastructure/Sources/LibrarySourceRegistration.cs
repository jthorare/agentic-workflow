using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Infrastructure.Docs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>Registers what the library feed and its status need from the environment.</summary>
internal static class LibrarySourceRegistration
{
    internal static IServiceCollection AddLibrarySources(this IServiceCollection services, Func<string, string?> environment) =>
        services
            .AddSingleton(LibraryHostFromEnvironment.Read(environment))
            .AddSingleton<ILibraryStatus, FileLibraryStatus>()
            .AddSingleton(sp => new LibraryParts(sp.GetRequiredService<LibraryHost>(), sp.GetRequiredService<ILibraryStatus>(), sp.GetRequiredService<TimeProvider>()));
}
