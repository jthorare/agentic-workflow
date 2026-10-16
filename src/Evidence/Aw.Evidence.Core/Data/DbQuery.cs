using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>One query of a database script: a single read statement and what its rows must hold.</summary>
/// <param name="Sql">Exactly one statement.</param>
/// <param name="Expect">What the rows must hold.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DbQuery(string Sql, DbExpectation Expect);
