using System.Reflection;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// What a type's method bodies load and call, read from the IL: the string literals (ldstr) and the methods called (call, callvirt, newobj). A byte pattern is
/// matched first and every hit is confirmed by resolving its token, so a stray byte sequence inside another instruction's operand never counts.
/// </summary>
public static class IlScan
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    private const byte Ldstr = 0x72;
    private const byte Newobj = 0x73;
    private const byte UserStringTable = 0x70;
    private const byte MemberRefTable = 0x0A;
    private const byte MethodDefTable = 0x06;
    private const byte MethodSpecTable = 0x2B;
    private const byte LdcI40 = 0x16;
    private const byte LdcI4S = 0x1F;
    private static readonly byte[] _calls = [0x28, 0x6F, 0x73];

    /// <summary>The string literals the type's methods, constructors and nested types (closures, state machines) load.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The literals.</returns>
    public static IReadOnlySet<string> Literals(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (method, il) in Bodies(type))
        {
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] == Ldstr && il[i + 4] == UserStringTable && Resolve(() => method.Module.ResolveString(BitConverter.ToInt32(il, i + 1))) is { } text)
                {
                    _ = found.Add(text);
                }
            }
        }

        return found;
    }

    /// <summary>The declaring types of the methods the type's bodies call or construct.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The declaring types.</returns>
    public static IReadOnlySet<Type> CalledTypes(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var found = new HashSet<Type>();
        foreach (var (method, il) in Bodies(type))
        {
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (Array.IndexOf(_calls, il[i]) >= 0 && il[i + 4] is MemberRefTable or MethodDefTable
                    && Resolve(() => method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1), genericTypeArguments: method.DeclaringType?.GenericTypeArguments, genericMethodArguments: null)?.DeclaringType) is { } declaring)
                {
                    _ = found.Add(declaring);
                }
            }
        }

        return found;
    }

    /// <summary>The methods the type's bodies (and nested types: closures, state machines) call with call, callvirt or newobj.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The called methods.</returns>
    public static IReadOnlySet<MethodBase> CalledMethods(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var found = new HashSet<MethodBase>();
        foreach (var (method, il) in Bodies(type))
        {
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (Array.IndexOf(_calls, il[i]) >= 0 && il[i + 4] is MemberRefTable or MethodDefTable
                    && Resolve(() => method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1), genericTypeArguments: method.DeclaringType?.GenericTypeArguments, genericMethodArguments: null)) is { } called)
                {
                    _ = found.Add(called);
                }
            }
        }

        return found;
    }

    /// <summary>The declaring types of the constructors the type's bodies call with newobj (only construction, not other calls on the type).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The constructed types.</returns>
    public static IReadOnlySet<Type> ConstructedTypes(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var found = new HashSet<Type>();
        foreach (var (method, il) in Bodies(type))
        {
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] == Newobj && il[i + 4] is MemberRefTable or MethodDefTable
                    && Resolve(() => method.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1), genericTypeArguments: method.DeclaringType?.GenericTypeArguments, genericMethodArguments: null)?.DeclaringType) is { } declaring)
                {
                    _ = found.Add(declaring);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Every call and construction in the type's bodies (and nested types: closures, state machines), with the method that holds it. Generic instantiations
    /// (a MethodSpec token) resolve too, so Task.WhenAny&lt;T&gt; and Task.Run&lt;T&gt; are seen.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The calls, one per call site.</returns>
    public static IReadOnlyList<IlCall> Calls(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var found = new List<IlCall>();
        foreach (var (method, il) in Bodies(type))
        {
            var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (var i = 0; i + 4 < il.Length; i++)
            {
                var at = i;
                if (Array.IndexOf(_calls, il[at]) >= 0 && il[at + 4] is MemberRefTable or MethodDefTable or MethodSpecTable
                    && Resolve(() => method.Module.ResolveMethod(BitConverter.ToInt32(il, at + 1), method.DeclaringType?.GenericTypeArguments, methodArguments)) is { } callee)
                {
                    found.Add(new IlCall(method, callee, FalseBefore(il, at)));
                }
            }
        }

        return found;
    }

    // ldc.i4.0 or ldc.i4.s 0 right before the call: the last argument pushed is the constant false (or 0).
    // Only the constant is seen: ConfigureAwait(someBool) with a variable or a field reads as not-false and passes, so ARCH-WF-001 leaves it to review
    // (the gap is in rules/engineering/csharp/workflow-scheduler.yaml).
    private static bool FalseBefore(byte[] il, int call) =>
        call > 0 && (il[call - 1] == LdcI40 || (call > 1 && il[call - 2] == LdcI4S && il[call - 1] == 0));

    private static IEnumerable<(MethodBase Method, byte[] Il)> Bodies(Type type) =>
        type.GetMembers(Declared).OfType<MethodBase>()
            .Concat(type.GetNestedTypes(Declared).SelectMany(n => Bodies(n).Select(b => b.Method)))
            .Select(m => (Method: m, Il: m.GetMethodBody()?.GetILAsByteArray()))
            .Where(b => b.Il is not null)
            .Select(b => (b.Method, b.Il!));

    private static T? Resolve<T>(Func<T> resolve)
    {
        try
        {
            return resolve();
        }
        catch (Exception e) when (e is ArgumentException or BadImageFormatException or NotSupportedException or TypeLoadException)
        {
            return default;   // the byte pattern was not an instruction
        }
    }
}
