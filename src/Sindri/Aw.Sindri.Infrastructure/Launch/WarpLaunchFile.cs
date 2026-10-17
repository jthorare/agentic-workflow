using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A Warp launch configuration written with the YAML emitter, never by string concatenation (UI spec 6.3 step 1).</summary>
internal static partial class WarpLaunchFile
{
    /// <summary>One window, one tab with the title, the repo path as its only path, and one 'exec' line.</summary>
    public static void Emit(TextWriter writer, string name, string title, string cwd, string exec)
    {
        var yaml = new Out(new Emitter(writer));
        yaml.Begin();
        yaml.Map(() =>
        {
            yaml.Pair("name", "sindri-" + name);
            yaml.Key("windows");
            yaml.Seq(() => yaml.Map(() =>
            {
                yaml.Key("tabs");
                yaml.Seq(() => yaml.Map(() =>
                {
                    yaml.Pair("title", title);
                    yaml.Key("layout");
                    yaml.Map(() =>
                    {
                        yaml.Pair("cwd", cwd);
                        yaml.Key("commands");
                        yaml.Seq(() => yaml.Map(() => yaml.Pair("exec", exec)));
                    });
                }));
            }));
        });
        yaml.End();
    }

    /// <summary>Single-quotes an argument for a POSIX shell unless it is plain.</summary>
    public static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return argument.Length > 0 && PlainWord.IsMatch(argument) ? argument : "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }

    [GeneratedRegex("^[A-Za-z0-9_@%+:,./-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PlainWord { get; }

    private sealed class Out(Emitter emitter)
    {
        public void Begin()
        {
            emitter.Emit(new StreamStart());
            emitter.Emit(new DocumentStart());
        }

        public void End()
        {
            emitter.Emit(new DocumentEnd(isImplicit: true));
            emitter.Emit(new StreamEnd());
        }

        public void Key(string key) => emitter.Emit(new Scalar(key));

        public void Text(string value) =>
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, value, ScalarStyle.DoubleQuoted, isPlainImplicit: true, isQuotedImplicit: true));

        public void Pair(string key, string value)
        {
            Key(key);
            Text(value);
        }

        public void Map(Action body)
        {
            emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, isImplicit: true, MappingStyle.Block));
            body();
            emitter.Emit(new MappingEnd());
        }

        public void Seq(Action body)
        {
            emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, isImplicit: true, SequenceStyle.Block));
            body();
            emitter.Emit(new SequenceEnd());
        }
    }
}
