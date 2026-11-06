namespace Aw.Context.Testing.Linear;

/// <summary>A field of a mutation input type: its name, its scalar type, whether it is required, and whether it is a list.</summary>
/// <param name="Name">The field name.</param>
/// <param name="Type">The scalar type name.</param>
/// <param name="Required">Whether the type is non-null.</param>
/// <param name="List">Whether the field is a list of non-null scalars.</param>
internal sealed record LinearInputField(string Name, string Type, bool Required = false, bool List = false);
