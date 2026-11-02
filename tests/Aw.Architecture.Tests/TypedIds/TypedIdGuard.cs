using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.Json.Serialization;
using Aw.Kernel.Ids;

namespace Aw.Architecture.Tests.TypedIds;

/// <summary>
/// The typed-id guard (docs/csharp/typed-ids.md). It catches the silent failures: an id that writes {} and
/// reads back uninitialized, and a typed-id property that a missing JSON property would leave uninitialized. It
/// matches Vogen's attributes by name and never references Vogen, so it can scan any assembly. ArchitectureTests runs
/// it over every registered Aw.* assembly (Packages.cs): ARCH-ID-001 for the defaults, ARCH-ID-002 for the types.
/// </summary>
internal static class TypedIdGuard
{
    private const string ValueObjectAttribute = "Vogen.ValueObjectAttribute";
    private const string GenericValueObjectAttribute = "Vogen.ValueObjectAttribute`1";
    private const string DefaultsAttribute = "Vogen.VogenDefaultsAttribute";

    /// <summary>The VogenDefaults arguments the recipe requires, by parameter name and enum member name.</summary>
    public static IReadOnlyDictionary<string, string> RequiredDefaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["conversions"] = "None",
        ["systemTextJsonConverterFactoryGeneration"] = "Omit",
        ["primitiveEqualityGeneration"] = "Omit",
        ["toPrimitiveCasting"] = "None",
        ["fromPrimitiveCasting"] = "None",
        ["tryFromGeneration"] = "GenerateBoolMethod",
    };

    /// <summary>Checks the assembly's [assembly: VogenDefaults(...)] (required in every assembly that declares ids).</summary>
    /// <param name="assembly">An assembly that declares Vogen ids.</param>
    /// <returns>One message per violation.</returns>
    public static IReadOnlyList<string> CheckDefaults(Assembly assembly)
    {
        var defaults = assembly.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == DefaultsAttribute);
        return defaults is null
            ? [$"{assembly.GetName().Name} has no [assembly: VogenDefaults(...)]; copy the recipe's VogenDefaults.cs."]
            : [.. RequiredDefaults
                .Select(r => (r.Key, Want: r.Value, Got: EnumArgument(defaults, r.Key)))
                .Where(r => !string.Equals(r.Got, r.Want, StringComparison.Ordinal))
                .Select(r => $"{assembly.GetName().Name}: VogenDefaults {r.Key} is {r.Got ?? "missing"}, the recipe requires {r.Want}.")];
    }

    /// <summary>Whether the assembly declares any Vogen value object (then it must carry the VogenDefaults block).</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>True when at least one type carries a ValueObject attribute.</returns>
    public static bool DeclaresIds(Assembly assembly) => assembly.GetTypes().Any(IsValueObject);

    /// <summary>
    /// Checks value objects (a [JsonConverter] in user code, <see cref="ITypedId{TSelf, TValue}"/>, no per-type
    /// conversions) and every typed-id property (required on read). Pass a Core assembly's types for the first, and
    /// a Features, Host or Infrastructure assembly's types for the second; both checks run on any type.
    /// </summary>
    /// <param name="types">The types to check.</param>
    /// <returns>One message per violation.</returns>
    public static IReadOnlyList<string> CheckTypes(IEnumerable<Type> types)
    {
        var list = types.ToList();
        return [.. list.Where(IsValueObject).SelectMany(ValueObjectViolations), .. list.SelectMany(PropertyViolations)];
    }

    private static IEnumerable<string> ValueObjectViolations(Type type)
    {
        var converter = type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;
        if (converter is null)
        {
            yield return $"{type.Name} has no [JsonConverter]; it would serialize as {{}} and read back uninitialized.";
        }
        else if (converter.DeclaringType == type || Attribute.IsDefined(converter, typeof(GeneratedCodeAttribute)))
        {
            yield return $"{type.Name} names a converter in generated code; the System.Text.Json source generator can't see it.";
        }

        if (!ImplementsTypedId(type))
        {
            yield return $"{type.Name} doesn't implement ITypedId<,>; the Kernel converters and the id port can't use it.";
        }

        var conversions = EnumArgument(ValueObjectData(type), "conversions");
        if (conversions is not null and not "Unspecified" and not "None")
        {
            yield return $"{type.Name} sets conversions: {conversions}; leave conversions to VogenDefaults (None).";
        }
    }

    private static IEnumerable<string> PropertyViolations(Type type)
    {
        // why: System.Text.Json never deserializes an abstract type or an interface, and an abstract type's constructor is emitted protected, so there is no read to require the id on
        if (type.IsAbstract)
        {
            return [];
        }

        var constructor = SerializerConstructor(type);
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(p => IsTypedId(p.PropertyType) && !IsRequiredOnRead(p, constructor))
            .Select(p => $"{type.Name}.{p.Name} is a typed id the JSON profile doesn't require; make it a constructor "
                + "parameter without a default, a 'required' member or [JsonRequired].");
    }

    /// <summary>The constructor System.Text.Json deserializes with: [JsonConstructor], else the only public one unless a parameterless one exists.</summary>
    private static ConstructorInfo? SerializerConstructor(Type type)
    {
        var all = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var marked = all.FirstOrDefault(c => Attribute.IsDefined(c, typeof(JsonConstructorAttribute)));
        if (marked is not null)
        {
            return marked;
        }

        var visible = all.Where(c => c.IsPublic).ToList();
        return visible.Count == 1 && visible[0].GetParameters().Length > 0 ? visible[0] : null;
    }

    private static bool IsRequiredOnRead(PropertyInfo property, ConstructorInfo? constructor) =>
        Attribute.IsDefined(property, typeof(JsonRequiredAttribute))
        || Attribute.IsDefined(property, typeof(System.Runtime.CompilerServices.RequiredMemberAttribute))
        || (constructor?.GetParameters()
            .Any(p => string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase) && !p.HasDefaultValue) ?? false);

    // A nullable id (ItemId?) reads a missing property as null, never as an uninitialized id.
    private static bool IsTypedId(Type type) => type.IsValueType && (IsValueObject(type) || ImplementsTypedId(type));

    private static bool ImplementsTypedId(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<,>));

    private static bool IsValueObject(Type type) => ValueObjectData(type) is not null;

    private static CustomAttributeData? ValueObjectData(Type type) =>
        type.GetCustomAttributesData().FirstOrDefault(a =>
            string.Equals(a.AttributeType.FullName, ValueObjectAttribute, StringComparison.Ordinal)
            || (a.AttributeType.IsGenericType && a.AttributeType.GetGenericTypeDefinition().FullName == GenericValueObjectAttribute));

    /// <summary>An enum argument's member name, by constructor parameter or named argument; null when absent.</summary>
    private static string? EnumArgument(CustomAttributeData? attribute, string parameter)
    {
        if (attribute is null)
        {
            return null;
        }

        var named = attribute.NamedArguments.FirstOrDefault(n => n.MemberName == parameter);
        if (named.MemberInfo is not null)
        {
            return Enum.GetName(named.TypedValue.ArgumentType, named.TypedValue.Value!);
        }

        var index = Array.FindIndex(attribute.Constructor.GetParameters(), p => p.Name == parameter);
        return index < 0
            ? null
            : Enum.GetName(attribute.ConstructorArguments[index].ArgumentType, attribute.ConstructorArguments[index].Value!);
    }
}
