using Aw.Forge.Core;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>A long-lived token in an environment variable (env:AW_FORGE_TOKEN), bound to one host: asked for any other host it hands out nothing.</summary>
internal sealed class EnvCredentialSource(string boundHost, string name, Func<string, string?> environment) : ICredentialSource
{
    public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken)
    {
        if (!BoundHost.Matches(boundHost, host))
        {
            return ValueTask.FromResult(Results.Failure<string>(BoundHost.Refusal(host)));
        }

        return ValueTask.FromResult(environment(name) is { Length: > 0 } token
            ? Results.Success(token.Trim())
            : Results.Failure<string>(ForgeErrors.Unauthorized.With($"{name} is not set")));
    }
}
