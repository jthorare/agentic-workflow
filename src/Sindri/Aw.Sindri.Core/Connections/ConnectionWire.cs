using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>The wire names of the connection enums: kinds 'oauth|mcp|rest|cli', statuses 'connected|needs-sign-in|error'.</summary>
public static class ConnectionWire
{
    /// <summary>A kind's wire name.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string Name(ConnectionKind kind) => throw new NotSupportedException();

    /// <summary>A status's wire name.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The name.</returns>
    public static string Name(ConnectionStatus status) => throw new NotSupportedException();

    /// <summary>Reads a kind.</summary>
    /// <param name="text">The wire name.</param>
    /// <returns>The kind, or SND-API-… invalid input.</returns>
    public static Result<ConnectionKind> ParseKind(string text) => throw new NotSupportedException();

    /// <summary>Reads a status.</summary>
    /// <param name="text">The wire name.</param>
    /// <returns>The status, or invalid input.</returns>
    public static Result<ConnectionStatus> ParseStatus(string text) => throw new NotSupportedException();
}
