using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>The endpoint record as &lt;home&gt;/control.json (0600), written atomically by the control plane once it listens.</summary>
internal sealed class FileEndpoint(SindriSettings settings, JsonSerializerOptions options) : IEndpointFile
{
    public async ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(settings.EndpointPath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return Results.Success<ControlEndpoint?>(value: null);
        }
        catch (DirectoryNotFoundException)
        {
            return Results.Success<ControlEndpoint?>(value: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable(e);
        }

        var endpoint = AwJson.Deserialize<ControlEndpoint>(bytes, options);
        return endpoint.IsError ? Results.Success<ControlEndpoint?>(value: null) : endpoint.Value;   // a damaged record reads as none
    }

    public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.WriteAtomically(settings.EndpointPath, AwJson.Serialize(endpoint, options));
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(Unreadable(e));
        }
    }

    public async ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationToken)
    {
        var current = await Read(cancellationToken).ConfigureAwait(false);
        if (current.IsError || current.Value?.Pid != pid)
        {
            return current.IsError ? current.Error : Unit.Value;
        }

        try
        {
            File.Delete(settings.EndpointPath);
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Unreadable(e);
        }
    }

    private Error Unreadable(Exception e) => SindriErrors.LedgerUnavailable.With(
        $"{settings.EndpointPath} can't be read or written: {e.Message} (fix: check the permissions of {settings.Home})");
}
