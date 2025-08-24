using System.ComponentModel;
using System.Xml;

namespace PolicyPlus
{
    public static class XmlExtensions
    {
        public static string? AttributeOrNull(this XmlNode Node, string Attribute)
        {
            var attrs = Node.Attributes;
            if (attrs is null)
                return null;
            var attr = attrs[Attribute];
            return attr?.Value;
        }

        public static object AttributeOrDefault(this XmlNode Node, string Attribute, object DefaultVal)
        {
            var attrs = Node.Attributes;
            if (attrs is null || attrs[Attribute] is null)
                return DefaultVal;
            var converter = TypeDescriptor.GetConverter(DefaultVal.GetType());
            var value = attrs[Attribute]!.Value;
            if (converter.IsValid(value))
            {
                var converted = converter.ConvertFromString(value);
                return converted ?? DefaultVal;
            }

            return DefaultVal;
        }
    }
}
