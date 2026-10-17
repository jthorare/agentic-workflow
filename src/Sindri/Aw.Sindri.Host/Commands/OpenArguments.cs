using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Commands;

/// <summary>The arguments of 'sindri open &lt;item&gt; [--agent &lt;id&gt;] [--attach | --resume] [--version N] [--no-wait] [--json] [--no-start]'.</summary>
internal sealed class OpenArguments
{
    internal const string Usage = "usage: sindri open <item> [--agent <id>] [--attach | --resume] [--version <n>] [--no-wait] [--json] [--no-start]";

    private OpenArguments()
    {
    }

    internal string Item { get; private set; } = string.Empty;

    internal LaunchMode Mode { get; private set; } = LaunchMode.Open;

    internal AgentId? Agent { get; private set; }

    internal long? Version { get; private set; }

    internal bool Json { get; private set; }

    internal bool NoWait { get; private set; }

    internal bool NoStart { get; private set; }

    internal static Result<OpenArguments> Parse(string[] args)
    {
        var open = new OpenArguments();
        var item = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var taken = open.Take(args, i, item);
            if (taken.IsError)
            {
                return taken.Error;
            }

            i += taken.Value;
        }

        if (item.Count != 1)
        {
            return SindriErrors.InvalidInput.With((item.Count == 0 ? "which item? " : "one item at a time. ") + Usage);
        }

        open.Item = item[0];
        return open;
    }

    /// <summary>Reads the argument at <paramref name="i"/>; the extra arguments it consumed.</summary>
    private Result<int> Take(string[] args, int i, List<string> words)
    {
        var value = i + 1 < args.Length ? args[i + 1] : null;
        if (Switch(args[i]))
        {
            return 0;
        }

        switch (args[i])
        {
            case "--attach" or "--resume":
                return Choose(args[i] == "--attach" ? LaunchMode.Attach : LaunchMode.Resume);
            case "--agent" when value is not null:
                return SetAgent(value);
            case "--version" when value is not null:
                return SetVersion(value);
            case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                return SindriErrors.InvalidInput.With($"'{flag}' isn't a flag of this command (or it needs a value) ({Usage})");
            case var word:
                words.Add(word);
                return 0;
        }
    }

    private bool Switch(string flag)
    {
        switch (flag)
        {
            case "--json":
                Json = true;
                return true;
            case "--no-wait":
                NoWait = true;
                return true;
            case "--no-start":
                NoStart = true;
                return true;
            default:
                return false;
        }
    }

    private Result<int> Choose(LaunchMode mode)
    {
        if (Mode != LaunchMode.Open && Mode != mode)
        {
            return SindriErrors.InvalidInput.With($"--attach and --resume exclude each other ({Usage})");
        }

        Mode = mode;
        return 0;
    }

    private Result<int> SetAgent(string value)
    {
        if (!AgentId.TryParse(value, provider: null, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{value}' isn't an agent id: a lowercase letter, then lowercase letters, digits or '-', 2 to 32 characters ({Usage})");
        }

        Agent = id;
        return 1;
    }

    private Result<int> SetVersion(string value)
    {
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return SindriErrors.InvalidInput.With($"'{value}' isn't an item version: a number from 1 ({Usage})");
        }

        Version = number;
        return 1;
    }
}
