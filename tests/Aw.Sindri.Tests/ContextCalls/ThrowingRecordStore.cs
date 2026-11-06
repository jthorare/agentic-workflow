using System.Reflection;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Records;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IRecordStore"/> whose <see cref="IRecordStore.List"/> throws what a row with a '' version threw before #1952's fix; every other call goes to an empty in-memory store.</summary>
public class ThrowingRecordStore : DispatchProxy
{
    private readonly FakeRecordStore _inner = new();

    /// <summary>How many list reads were made.</summary>
    public int Listed { get; private set; }

    internal static IRecordStore Create() => Create<IRecordStore, ThrowingRecordStore>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IRecordStore.List))
        {
            Listed++;
            _ = SourceVersion.From(string.Empty);
        }

        return targetMethod?.Invoke(_inner, args);
    }
}
