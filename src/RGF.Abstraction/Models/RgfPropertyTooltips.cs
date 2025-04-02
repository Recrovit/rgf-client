using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

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

    [XmlElement("Properties")]
    public RgfPropertiesTooltips Properties { get; set; }

    [XmlElement("Dictionaries")]
    public RgfDictionariesTooltips Dictionaries { get; set; }

    public string GetTooltip(int propertyId, string key = null)
    {
        if (!string.IsNullOrEmpty(key) && Dictionaries?.DictionaryList.FirstOrDefault(d => d.Id == propertyId)?.Items.FirstOrDefault(i => i.Key == key) is { } itemTooltip)
        {
            return itemTooltip.Value;
        }
        if (Properties?.PropertyList.FirstOrDefault(p => p.Id == propertyId) is { } propertyTooltip)
        {
            return propertyTooltip.Value;
        }
        return null;
    }

    //[XmlAnyElement]
    //public XmlElement[] RawTooltipElements
    //{
    //    get => null;
    //    set
    //    {
    //        if (value != null)
    //        {
    //            foreach (var element in value) { }
    //        }
    //    }
    //}
}

public class RgfPropertiesTooltips
{
    [XmlElement("Property")]
    public List<RgfPropertyTooltip> PropertyList { get; set; } = new();
}

public class RgfPropertyTooltip
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlText]
    public string Value { get; set; }
}

public class RgfDictionariesTooltips
{
    [XmlElement("Dictionary")]
    public List<RgfDictionaryTooltips> DictionaryList { get; set; } = new();
}

public class RgfDictionaryTooltips
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlElement("Item")]
    public List<RgfDictionaryItemTooltip> Items { get; set; } = new();
}

public class RgfDictionaryItemTooltip
{
    [XmlAttribute("key")]
    public string Key { get; set; }

    [XmlText]
    public string Value { get; set; }
}