using System.Globalization;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>Reads a database script document and checks its rules.</summary>
public static class DbScriptReader
{
    /// <summary>Parses and validates a script.</summary>
    /// <param name="utf8Json">The script file's bytes.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The script, or AW-EVIDENCE-018 naming the JSON location and every problem.</returns>
    public static Result<DbScript> Read(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options) =>
        JsonInput.Read<DbScript>(utf8Json, options, EvidenceErrors.DbScriptInvalid).Bind(Validate);

    private static Result<DbScript> Validate(DbScript script)
    {
        var problems = new Problems();
        problems.AddIf(script.Schema is not (null or DbScript.SchemaName), $"$.schema: must be {DbScript.SchemaName} when given");
        problems.AddIf(script.Connection is not null && script.Sqlite is not null, "$: connection or sqlite: give one, not both");
        problems.AddIf(script.Connection is null && script.Sqlite is null, "$: connection or sqlite: give one");
        problems.AddIf(script.Connection is { Length: 0 }, "$.connection: must name an entry of the targets config");
        problems.AddIf(script.Sqlite is { Length: 0 }, "$.sqlite: must name a file");
        problems.AddIf(script.Seed is { Length: 0 }, "$.seed: must name a file");
        problems.AddIf(script.Seed is not null && script.Connection is not null, "$.seed: a seed is for a sqlite file only; a connection is never seeded");
        problems.AddIf(script.RowLimit is not null and not (>= 1 and <= DbScript.MaxRowLimit), $"$.rowLimit: must be 1 to {DbScript.MaxRowLimit.ToString(CultureInfo.InvariantCulture)}");
        problems.AddIf(script.QueryList.Count == 0, "$.queries: needs at least one query");
        for (var i = 0; i < script.QueryList.Count; i++)
        {
            CheckQuery(script.QueryList[i], ApiScriptReader.Item("$.queries", i), script.Sqlite is null, problems);
        }

        return problems.Into(script, EvidenceErrors.DbScriptInvalid);
    }

    private static void CheckQuery(DbQuery query, string at, bool nestedComments, Problems problems)
    {
        var statements = SqlText.Statements(query.Sql ?? string.Empty, nestedComments);
        problems.AddIf(statements.Count != 1, $"{at}.sql: must be exactly one statement");
        var expect = query.Expect;
        var pointers = expect?.Pointers ?? [];
        problems.AddIf(expect is null || (expect.RowCount is null && pointers.Count == 0 && expect.Golden is null), $"{at}.expect: give at least one of rowCount, pointers and golden");
        if (expect is null)
        {
            return;
        }

        problems.AddIf(expect.RowCount is < 0, $"{at}.expect.rowCount: must be 0 or more");
        problems.AddIf(expect.Golden is { Length: 0 }, $"{at}.expect: golden must name a file");
        for (var i = 0; i < pointers.Count; i++)
        {
            ApiScriptReader.CheckPointer(pointers[i], ApiScriptReader.Item(at + ".expect.pointers", i), problems);
        }
    }
}
