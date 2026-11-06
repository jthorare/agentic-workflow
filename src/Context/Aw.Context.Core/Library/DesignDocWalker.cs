namespace Aw.Context.Core.Library;

/// <summary>Walks a design doc's body: finds the items (headings, list items, table rows), their refs and the body's errors (011, 013, 014).</summary>
internal sealed partial class DesignDocWalker(string path, IReadOnlyList<string> lines, int bodyStart, string? docKind)
{
    private enum Source
    {
        Heading,
        List,
        Table,
    }

    private sealed class Candidate(string key, DesignItemKind kind, string? parent, int block, int line, Source source, string? reqAbove)
    {
        internal string Key { get; } = key;
        internal DesignItemKind Kind { get; } = kind;
        internal string? Parent { get; set; } = parent;
        internal int Block { get; } = block;
        internal int Line { get; } = line;
        internal Source Source { get; } = source;
        internal string? ReqAbove { get; } = reqAbove;
    }

    private readonly record struct Frame(int Level, string? Key);

    private readonly List<Candidate> _candidates = [];
    private readonly Dictionary<string, Candidate> _defined = new(StringComparer.Ordinal);
    private readonly List<DesignRef> _refs = [];
    private readonly List<DesignDocError> _errors = [];
    private readonly List<Frame> _stack = [];
    private readonly Dictionary<string, string> _pairs = new(StringComparer.Ordinal);
    private List<Block> _blocks = [];
    private int _headings;
    private (int Heading, string Key)? _lastRequirement;
    private string? _currentTask;

    /// <summary>The number of items found (before the limit is applied).</summary>
    internal int ItemCount => _candidates.Count;

    /// <summary>The number of distinct refs found (before the limit is applied).</summary>
    internal int RefCount { get; private set; }

    /// <summary>Walks the body.</summary>
    /// <returns>The items and refs (empty when the body has an error), and the body's errors.</returns>
    internal (List<DesignItem> Items, List<DesignRef> Refs, List<DesignDocError> Errors) Walk()
    {
        _blocks = Scan();
        for (var bi = 0; bi < _blocks.Count; bi++)
        {
            var b = _blocks[bi];
            if (b.Kind == BlockKind.Heading)
            {
                OnHeading(b, bi);
            }
            else if (b.Kind == BlockKind.ListItem)
            {
                OnListItem(b, bi);
            }
            else
            {
                OnTableRow(b, bi);
            }
        }

        ResolveAcceptance();
        var refs = _refs.Distinct().ToList();
        RefCount = refs.Count;
        return _errors.Count > 0 || _candidates.Count > DesignDocParser.MaxItems || refs.Count > DesignDocParser.MaxRefs
            ? ([], [], _errors)
            : ([.. _candidates.Select(ToItem)], refs, _errors);
    }

    private static bool IsDefinedIn(string? kind, DesignItemKind item) => (item, kind) switch
    {
        (DesignItemKind.Phase, "spec" or "tdd" or "stages") => true,
        (DesignItemKind.Requirement or DesignItemKind.Acceptance, "spec" or "tdd" or "product") => true,
        (DesignItemKind.TestPlan, "spec" or "tdd" or "verification") => true,
        (DesignItemKind.Task, "tasks") => true,
        _ => false,
    };

    private bool Defines(DesignItemKind item) => IsDefinedIn(docKind, item);

    private void OnHeading(Block b, int bi)
    {
        _headings++;
        _currentTask = null;
        var slug = NextSlug(DesignIds.Slug(b.Content));
        while (_stack.Count > 0 && _stack[^1].Level >= b.Level)
        {
            _stack.RemoveAt(_stack.Count - 1);
        }

        var key = b.Level is >= 2 and <= 4 ? DefineHeading(b, bi, slug) : null;
        _stack.Add(new Frame(b.Level, key));
    }

    private string? DefineHeading(Block b, int bi, string slug)
    {
        var lead = DesignIds.Lead(b.Content);
        if (lead.Shape == IdShape.Bad)
        {
            ReportBadId(b, lead);
            return null;
        }

        if (lead.Shape == IdShape.Valid && Defines(lead.Kind))
        {
            if (!_defined.TryGetValue(lead.Key, out var prior))
            {
                Add(lead.Key, lead.Kind, Nearest(), bi, Source.Heading);
                _currentTask = lead.Kind == DesignItemKind.Task ? lead.Key : null;
                return lead.Key;
            }

            if (prior.Source == Source.Heading)
            {
                Duplicate(lead.Key, prior, b.Index);
                return null;
            }
        }

        if (slug.Length == 0)
        {
            return null;
        }

        Add(slug, DesignItemKind.Section, Nearest(), bi, Source.Heading);
        return slug;
    }

    private void OnListItem(Block b, int bi)
    {
        var lead = DesignIds.Lead(b.Content);
        if (lead.Shape == IdShape.Bad)
        {
            ReportBadId(b, lead);
        }
        else if (lead.Shape == IdShape.Valid)
        {
            OnListId(b, bi, lead);
        }
        else
        {
            OnKeyLine(b);
        }
    }

    private void OnListId(Block b, int bi, IdLead lead)
    {
        if (!Defines(lead.Kind))
        {
            AddRef(Nearest(), lead.Key, DesignRefKind.Relates, b.Index);
        }
        else if (!_defined.TryGetValue(lead.Key, out var prior))
        {
            Add(lead.Key, lead.Kind, Nearest(), bi, Source.List);
        }
        else if (prior.Source == Source.List)
        {
            Duplicate(lead.Key, prior, b.Index);
        }
        else
        {
            AddRef(Nearest(), lead.Key, DesignRefKind.Relates, b.Index);
        }
    }

    private void OnKeyLine(Block b)
    {
        var keyLine = KeyLine.Match(DesignIds.StripBold(b.Content));
        if (!keyLine.Success || _currentTask is not { } task)
        {
            return;
        }

        var kind = string.Equals(keyLine.Groups["key"].Value, "depends", StringComparison.OrdinalIgnoreCase) ? DesignRefKind.Blocks : DesignRefKind.Relates;
        foreach (var mention in DesignIds.Mentions(keyLine.Groups["rest"].Value))
        {
            AddRef(task, mention.To, kind, b.Index);
        }
    }

    private void OnTableRow(Block b, int bi)
    {
        var lead = DesignIds.Lead(b.Cells[0]);
        if (lead.Shape == IdShape.Bad)
        {
            ReportBadId(b, lead);
            return;
        }

        if (lead.Shape != IdShape.Valid)
        {
            return;
        }

        var others = b.Cells.Skip(1).SelectMany(DesignIds.Mentions).ToList();
        var defines = Defines(lead.Kind);
        if (defines && !_defined.ContainsKey(lead.Key))
        {
            Add(lead.Key, lead.Kind, Nearest(), bi, Source.Table);
        }

        var verifies = defines && lead.Kind == DesignItemKind.TestPlan;
        foreach (var other in others)
        {
            AddRef(lead.Key, other.To, verifies ? DesignRefKind.Verifies : DesignRefKind.Relates, b.Index);
        }

        if (!verifies)
        {
            PairAcceptance(lead, others);
        }
    }

    private void PairAcceptance(IdLead lead, List<IdMention> others)
    {
        var mentioned = new List<IdMention> { new(lead.Key, lead.Kind, lead.Key) };
        mentioned.AddRange(others);
        var local = mentioned.Where(static m => !m.To.Contains('#', StringComparison.Ordinal)).ToList();
        var requirement = local.Where(static m => m.Kind == DesignItemKind.Requirement).Select(static m => m.Key).FirstOrDefault();
        if (requirement is null)
        {
            return;
        }

        foreach (var ac in local.Where(static m => m.Kind == DesignItemKind.Acceptance))
        {
            _ = _pairs.TryAdd(ac.Key, requirement);
        }
    }

    private void Add(string key, DesignItemKind kind, string? parent, int block, Source source)
    {
        string? reqAbove = null;
        if (kind == DesignItemKind.Acceptance && _lastRequirement is { } last && last.Heading == _headings)
        {
            reqAbove = last.Key;
        }

        var candidate = new Candidate(key, kind, parent, block, _blocks[block].Index + 1, source, reqAbove);
        _candidates.Add(candidate);
        _defined[key] = candidate;
        if (kind == DesignItemKind.Requirement)
        {
            _lastRequirement = (_headings, key);
        }

        if (kind == DesignItemKind.Task && source != Source.Heading)
        {
            _currentTask = key;
        }
    }

    private void ResolveAcceptance()
    {
        foreach (var c in _candidates.Where(static c => c.Kind == DesignItemKind.Acceptance))
        {
            c.Parent = _pairs.TryGetValue(c.Key, out var paired) ? paired : c.ReqAbove;
            if (c.Parent is null)
            {
                Report(DesignDocCodes.OrphanAcceptance, c.Line - 1, $"{c.Key} has no requirement: no table row pairs it and none precedes it in its section.", "Put it under its requirement, or add a Traceability row.");
            }
        }
    }

    private void ReportBadId(Block b, IdLead lead) =>
        Report(DesignDocCodes.BadItemId, b.Index, $"\"{lead.Key}\" is not a valid item id.", "Write the id as Ph<n>, REQ-<n>, REQ-<U|E|S|O|W><n>, AC-<n>, TP-<n> or TASK-<n>.");

    private void Duplicate(string key, Candidate prior, int index) =>
        Report(DesignDocCodes.DuplicateItem, index, $"{key} is defined twice (lines {FrontMatterBlock.Number(prior.Line)} and {FrontMatterBlock.Number(index + 1)}).", "Rename one, or turn the second into a reference.");

    private void Report(string code, int index, string what, string fix) =>
        _errors.Add(FrontMatterBlock.Error(path, index + 1, code, what, fix));

    private void AddRef(string? from, string to, DesignRefKind kind, int index) =>
        _refs.Add(new DesignRef(from, to, kind, index + 1));

    private string? Nearest()
    {
        for (var i = _stack.Count - 1; i >= 0; i--)
        {
            if (_stack[i].Key is { } key)
            {
                return key;
            }
        }

        return null;
    }
}
