using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>Who a database server must say it is before any script query runs (a tunnel or a reused port must not lead to another database).</summary>
/// <param name="ServerAddress">What inet_server_addr() must report (the address the server sees the connection arrive on).</param>
/// <param name="Database">What current_database() must report.</param>
/// <param name="ServerPort">What inet_server_port() must report, when given.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ServerIdentity(string ServerAddress, string Database, int? ServerPort = null);
