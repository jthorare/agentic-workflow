namespace Aw.Evidence.Core.Data;

/// <summary>What a server says about itself.</summary>
/// <param name="Address">inet_server_addr(), or null when the connection is not over TCP.</param>
/// <param name="Port">inet_server_port(), or null.</param>
/// <param name="Database">current_database().</param>
public sealed record ReportedServer(string? Address, int? Port, string Database);
