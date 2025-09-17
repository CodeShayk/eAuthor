using System.Xml;
using System.Xml.Schema;
using eAuthor.Models;
using Microsoft.Extensions.Caching.Memory;

namespace eAuthor.Services.Impl;

public class XsdService : IXsdService
{
    private readonly IMemoryCache _cache;

    public XsdService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public XsdNode ParseXsd(string xsd)
    {
        var key = "xsd:" + xsd.GetHashCode();
        if (_cache.TryGetValue<XsdNode>(key, out var cached) && cached != null)
            return cached;

        var set = new XmlSchemaSet();
        using var reader = XmlReader.Create(new StringReader(xsd));
        var schema = XmlSchema.Read(reader, (s, e) => { }) ?? throw new InvalidOperationException("schema is invalid");

        set.Add(schema);
        set.Compile();

        var rootEl = schema.Items.OfType<XmlSchemaElement>().FirstOrDefault()
            ?? throw new InvalidOperationException("No root element found.");
        var rootNode = new XsdNode
        {
            Name = rootEl.Name ?? "Root",
            Type = rootEl.ElementSchemaType?.Name ?? "complex",
            Path = "/" + (rootEl.Name ?? "Root")
        };
        if (rootEl.ElementSchemaType is XmlSchemaComplexType ct)
            ExtractChildren(ct, rootNode, rootNode.Path);

        _cache.Set(key, rootNode, TimeSpan.FromMinutes(30));
        return rootNode;
    }

    private void ExtractChildren(XmlSchemaComplexType complexType, XsdNode parent, string parentPath)
    {
        if (complexType.ContentTypeParticle is XmlSchemaSequence seq)
            foreach (var item in seq.Items.OfType<XmlSchemaElement>())
            {
                var node = new XsdNode
                {
                    Name = item.Name ?? "Unnamed",
                    Type = item.ElementSchemaType?.TypeCode.ToString() ?? "string",
                    IsArray = item.MaxOccurs > 1 || item.MaxOccursString == "unbounded",
                    Path = parentPath + "/" + item.Name
                };
                parent.Children.Add(node);
                if (item.ElementSchemaType is XmlSchemaComplexType ct)
                    ExtractChildren(ct, node, node.Path);
            }
    }
}