using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// IConnectionSecrets over the existing secret writer and pointer resolver: ONE secret per connection, the <see cref="TokenSet"/> as JSON then
/// base64url (the keychain writer's plain-token rule rejects JSON characters; base64url passes it). Service 'sindri-connection', account = the
/// name with ':' as '.'. Delete runs 'security delete-generic-password' (macOS) or 'secret-tool clear' (Linux) through the tool runner; a file
/// pointer's file is deleted. No token is in an argument list or in any error text.
/// </summary>
/// <param name="writer">Stores the secret (keychain, else an owner-only file).</param>
/// <param name="pointers">Reads it back.</param>
/// <param name="runner">Runs the delete tool.</param>
/// <param name="platform">Which store to delete from; null takes this machine's.</param>
internal sealed class KeychainConnectionSecrets(ISecretWriter writer, ISecretPointers pointers, IToolRunner runner, KeychainPlatform? platform = null) : IConnectionSecrets
{
    private readonly ISecretWriter _writer = writer;
    private readonly ISecretPointers _pointers = pointers;
    private readonly IToolRunner _runner = runner;
    private readonly KeychainPlatform? _platform = platform;

    public Result<SecretPointer> Store(ConnectionName name, TokenSet tokens) => throw Pending();

    public Result<TokenSet> Read(SecretPointer pointer) => throw Pending();

    public Result<Unit> Delete(SecretPointer pointer) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_writer, _pointers, _runner, _platform);
        return new NotSupportedException();
    }
}
