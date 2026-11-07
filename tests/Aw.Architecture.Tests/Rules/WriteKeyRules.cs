using System.Reflection;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// The Linear write key stays out of every read path (Linear hub spec 2026-11-04 D3, §9): of everything in the Context infrastructure, only the write adapter (and the key type itself) names the type that holds the resolved
/// write key. Looked at three ways: a field, property, parameter or return type of the key type; a base type or interface; and a call to, or construction of, one of its members read from the IL (a closure or state machine counts for the type it sits in).
/// </summary>
public static class WriteKeyRules
{
    /// <summary>A type other than the write adapter names the write key.</summary>
    public const string Id = "ARCH-LINEAR-001";

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>Checks the types.</summary>
    /// <param name="types">Every type of the assembly in scope.</param>
    /// <param name="key">The full name of the write key type.</param>
    /// <param name="holder">The full name of the one type that may use it.</param>
    /// <returns>One violation per offending top-level type.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Type> types, string key, string holder)
    {
        ArgumentNullException.ThrowIfNull(types);
        var all = types.ToList();
        return all
            .Where(t => !t.IsNested && !string.Equals(t.FullName, key, StringComparison.Ordinal) && !string.Equals(t.FullName, holder, StringComparison.Ordinal)
                && Nest(t, all).Any(n => Names(n, key)))
            .Select(t => new Violation(Id, $"{t.FullName} names {key}",
                $"only {holder} may hold the write key; read paths take the read key (LinearReadKey), and a new write path goes through {holder}"));
    }

    private static IEnumerable<Type> Nest(Type top, List<Type> all) => all.Where(t => t == top || IsInside(t, top));

    private static bool IsInside(Type type, Type top)
    {
        for (var outer = type.DeclaringType; outer is not null; outer = outer.DeclaringType)
        {
            if (outer == top)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Names(Type type, string key) =>
        Mentions(type.BaseType, key)
        || type.GetInterfaces().Any(i => Mentions(i, key))
        || type.GetFields(Declared).Any(f => Mentions(f.FieldType, key))
        || type.GetProperties(Declared).Any(p => Mentions(p.PropertyType, key))
        || type.GetMethods(Declared).Any(m => Mentions(m.ReturnType, key) || m.GetParameters().Any(p => Mentions(p.ParameterType, key)))
        || type.GetConstructors(Declared).Any(c => c.GetParameters().Any(p => Mentions(p.ParameterType, key)))
        || IlScan.CalledTypes(type).Any(c => Mentions(c, key));

    private static bool Mentions(Type? type, string key) =>
        type is not null
        && (string.Equals(type.FullName, key, StringComparison.Ordinal)
            || (type.HasElementType && Mentions(type.GetElementType(), key))
            || type.GenericTypeArguments.Any(a => Mentions(a, key)));
}
