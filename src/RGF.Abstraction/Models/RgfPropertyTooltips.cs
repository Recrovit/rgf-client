using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models
{
    [XmlRoot("Tooltip")]
    public class RgfPropertyTooltips
    {
        public static bool Deserialize(string xmlString, out RgfPropertyTooltips tooltip)
        {
            if (xmlString != null)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(RgfPropertyTooltips));
                    using var reader = new StringReader(xmlString);
                    tooltip = (RgfPropertyTooltips)serializer.Deserialize(reader);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error during deserialization: {ex.Message}");
                }
            }
            tooltip = null;
            return false;
        }

        [XmlAttribute("id")]
        public int EntityId { get; set; }

        [XmlIgnore]
        public Dictionary<int, string> Tooltips { get; set; } = new Dictionary<int, string>();

        public string GetTooltip(int key)
        {
            if (Tooltips.TryGetValue(key, out string tooltip))
            {
                return tooltip;
            }
            return null;
        }

        [XmlAnyElement]
        public XmlElement[] RawTooltipElements
        {
            get => null;
            set
            {
                if (value != null)
                {
                    foreach (var element in value)
                    {
                        var keyString = element.Name.Substring("rg-col-".Length);
                        if (int.TryParse(keyString, out int key))
                        {
                            Tooltips[key] = element.InnerText.Trim();
                        }
                        else
                        {
                            Console.Error.WriteLine($"Invalid key format: {element.Name}");
                        }
                    }
                }
            }
        }
    }
}