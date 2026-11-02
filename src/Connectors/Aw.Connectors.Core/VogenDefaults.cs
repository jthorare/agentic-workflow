using Vogen;

// No Vogen conversions: the System.Text.Json source generator can't see Vogen's converters and silently writes {}.
// The rest keeps an id's public surface to what ITypedId needs (Value, the bool TryFrom, IParsable).
[assembly: VogenDefaults(
    conversions: Conversions.None,
    systemTextJsonConverterFactoryGeneration: SystemTextJsonConverterFactoryGeneration.Omit,
    primitiveEqualityGeneration: PrimitiveEqualityGeneration.Omit,
    toPrimitiveCasting: CastOperator.None,
    fromPrimitiveCasting: CastOperator.None,
    tryFromGeneration: TryFromGeneration.GenerateBoolMethod)]
