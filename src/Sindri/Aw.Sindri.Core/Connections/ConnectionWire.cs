using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>The wire names of the connection enums: kinds 'oauth|mcp|rest|cli', statuses 'connected|needs-sign-in|error'.</summary>
public static class ConnectionWire
{
    /// <summary>A kind's wire name.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string Name(ConnectionKind kind) => kind switch
    {
        ConnectionKind.OAuth => "oauth",
        ConnectionKind.Mcp => "mcp",
        ConnectionKind.Rest => "rest",
        ConnectionKind.Cli => "cli",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>A status's wire name.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The name.</returns>
    public static string Name(ConnectionStatus status) => status switch
    {
        ConnectionStatus.Connected => "connected",
        ConnectionStatus.NeedsSignIn => "needs-sign-in",
        ConnectionStatus.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>Reads a kind.</summary>
    /// <param name="text">The wire name.</param>
    /// <returns>The kind, or SND-API-… invalid input.</returns>
    public static Result<ConnectionKind> ParseKind(string text) => text switch
    {
        "oauth" => ConnectionKind.OAuth,
        "mcp" => ConnectionKind.Mcp,
        "rest" => ConnectionKind.Rest,
        "cli" => ConnectionKind.Cli,
        _ => SindriErrors.InvalidInput.With("a connection kind is oauth, mcp, rest or cli"),
    };

    /// <summary>Reads a status.</summary>
    /// <param name="text">The wire name.</param>
    /// <returns>The status, or invalid input.</returns>
    public static Result<ConnectionStatus> ParseStatus(string text) => text switch
    {
        "connected" => ConnectionStatus.Connected,
        "needs-sign-in" => ConnectionStatus.NeedsSignIn,
        "error" => ConnectionStatus.Error,
        _ => SindriErrors.InvalidInput.With("a connection status is connected, needs-sign-in or error"),
    };
}
