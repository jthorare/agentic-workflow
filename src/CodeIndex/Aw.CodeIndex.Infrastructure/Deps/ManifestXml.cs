using System.Xml;
using System.Xml.Linq;

namespace Aw.CodeIndex.Infrastructure.Deps;

/// <summary>Shared reading for the MSBuild manifests: DTDs refused, namespaces ignored, a malformed file gives nothing.</summary>
internal static class ManifestXml
{
    private static readonly XmlReaderSettings _settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

    /// <summary>Parses the text, or returns null for malformed XML or one with a DTD.</summary>
    internal static XElement? Parse(string text)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), _settings);
            return XDocument.Load(reader).Root;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>The elements of a local name anywhere under the root, in document order.</summary>
    internal static IEnumerable<XElement> Items(XElement root, string localName) =>
        root.Descendants().Where(e => e.Name.LocalName == localName);

    /// <summary>An attribute's value by local name, or an empty string.</summary>
    internal static string Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    /// <summary>An attribute's value, or else the same-named child element's text.</summary>
    internal static string AttributeOrChild(XElement element, string name)
    {
        var value = Attribute(element, name);
        return value.Length > 0 ? value : element.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value.Trim() ?? string.Empty;
    }

    /// <summary>Whether the element or an enclosing element has a Condition.</summary>
    internal static bool IsConditional(XElement element) =>
        element.AncestorsAndSelf().Any(e => Attribute(e, "Condition").Length > 0);

    internal static bool NameIs(string path, string fileName) =>
        string.Equals(path[(path.LastIndexOf('/') + 1)..], fileName, StringComparison.OrdinalIgnoreCase);
}
