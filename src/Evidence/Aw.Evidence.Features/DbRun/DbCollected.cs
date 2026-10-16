using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.DbRun;

/// <summary>What the run's queries produced so far: the steps, the failed assertions, how many assertions there were, the first failure and what stopped the run.</summary>
internal sealed class DbCollected
{
    private const int TargetLimit = 200;

    private readonly ITextScrubber _initial;
    private readonly HashSet<string> _secretValues = new(StringComparer.Ordinal);

    internal DbCollected(ITextScrubber scrubber)
    {
        _initial = scrubber;
        Scrubber = scrubber;
    }

    /// <summary>The run's scrubber plus, by value, everything read so far from a secret-named column.</summary>
    internal ITextScrubber Scrubber { get; private set; }

    internal List<DbStep> Steps { get; } = [];

    internal List<ApiFailure> Failures { get; } = [];

    internal int Assertions { get; private set; }

    internal int? FirstFailedAssertion { get; private set; }

    internal string? FirstFailureText { get; private set; }

    internal CodedMessage? Refusal { get; set; }

    internal CodedMessage? Error { get; set; }

    internal bool Stopped => Refusal is not null || Error is not null;

    internal string TargetOf(string sql) => RecordedText.Of(Scrubber, string.Join(' ', sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), TargetLimit);

    internal void Skip(int index, string sql) => Steps.Add(new DbStep(index, DbQueryRunner.Action, TargetOf(sql), DbStepStatus.Skipped, RowCount: null, Truncated: false, Output: null));

    internal void Stop(int index, string sql, DbStepStatus status, Kernel.Error error)
    {
        var coded = new CodedMessage(error.Code, Scrubber.Scrub(error.Message));
        if (status == DbStepStatus.Refused)
        {
            Refusal = coded;
        }
        else
        {
            Error = coded;
        }

        Steps.Add(new DbStep(index, DbQueryRunner.Action, TargetOf(sql), status, RowCount: null, Truncated: false, Output: null));
    }

    internal void Record(int index, string sql, DbExpectation expect, Rows rows, string? goldenText)
    {
        Learn(rows);
        var json = rows.ToJson();
        var scrubber = KeysScrubber(rows);
        var checkedRows = DbExpectationCheck.Evaluate(index, expect, rows.Count, json, goldenText);
        var failures = checkedRows.Failures.Select(failure => RecordedText.Of(scrubber, failure)).ToList();
        if (checkedRows.FirstFailedOrdinal is { } ordinal && FirstFailedAssertion is null)
        {
            FirstFailedAssertion = Assertions + ordinal;
            FirstFailureText = $"query {Number.Text(index)}: {failures[0].Describe()}";
        }

        Assertions += checkedRows.Count;
        Failures.AddRange(failures);
        var status = failures.Count == 0 ? DbStepStatus.Passed : DbStepStatus.Failed;
        Steps.Add(new DbStep(index, DbQueryRunner.Action, TargetOf(sql), status, rows.Count, rows.Truncated, RecordedText.Of(scrubber, json, RecordedText.OutputLimit)));
    }

    // A column the result repeats gets a numbered key (cookie_2); it keeps hiding by the name it was renamed from, and by the table column it reads.
    private SecretKeysScrubber KeysScrubber(Rows rows)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var named = rows.Keys;
        for (var column = 0; column < named.Count; column++)
        {
            if (IsSecretColumn(rows, column))
            {
                _ = keys.Add(named[column]);
            }
        }

        return new SecretKeysScrubber(Scrubber, keys);
    }

    // A value read from a secret-named column (by its own name or the table column it reads) is hidden by value everywhere it is recorded from now on.
    private void Learn(Rows rows)
    {
        var added = false;
        for (var column = 0; column < rows.Columns.Count; column++)
        {
            if (IsSecretColumn(rows, column))
            {
                foreach (var row in rows.Values)
                {
                    added |= Remember(column < row.Count ? row[column] : null);
                }
            }
        }

        if (added)
        {
            Scrubber = new FieldValueScrubber(_initial, _secretValues);
        }
    }

    private bool IsSecretColumn(Rows rows, int column)
    {
        var source = rows.SourceColumns is { } names && column < names.Count ? names[column] : null;
        return Scrubber.IsSecretName(rows.Columns[column]) || (source is not null && Scrubber.IsSecretName(source));
    }

    // Every scalar in the cell, as the JSON text of the rows writes it: bytes as \x-hex, arrays element by element, dates and times in ISO.
    private bool Remember(object? value)
    {
        var added = false;
        switch (value)
        {
            case null or DBNull or bool:
                break;
            case byte[] bytes:
                added = _secretValues.Add("\\x" + Convert.ToHexStringLower(bytes));
                break;
            case string text:
                added = _secretValues.Add(text);
                break;
            case System.Collections.IEnumerable items:
                foreach (var item in items)
                {
                    added |= Remember(item);
                }

                break;
            default:
                added = _secretValues.Add(Text(value));
                break;
        }

        return added;
    }

    private static string Text(object value) => value switch
    {
        DateTime moment => ValueText.Moment(moment),
        DateTimeOffset moment => ValueText.Moment(moment),
        DateOnly day => ValueText.Day(day),
        TimeOnly time => ValueText.Time(time),
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
