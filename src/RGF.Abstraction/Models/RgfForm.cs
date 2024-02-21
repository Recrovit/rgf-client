using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

[XmlRoot("tabs")]
public class RgfForm
{
    public static bool Deserialize(string xmlString, out RgfForm tabs)
    {
        if (xmlString != null)
        {
            var serializer = new XmlSerializer(typeof(RgfForm));
            using (var reader = new StringReader(xmlString))
            {
                tabs = serializer.Deserialize(reader) as RgfForm;
                if (tabs != null)
                {
                    return true;
                }
            }
        }
        tabs = null;
        return false;
    }

    [XmlElement("tab")]
    public List<Tab> FormTabs { get; set; }

    public class Tab
    {
        [XmlAttribute("idx")]
        public int Index { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlArray("groups")]
        [XmlArrayItem("group")]
        public List<Group> Groups { get; set; }
    }

    public class Group
    {
        [XmlAttribute("idx")]
        public int Index { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlIgnore]
        public int FlexColumnWidth { get; set; }

        [XmlArray("properties")]
        [XmlArrayItem("property")]
        public List<Property> Properties { get; set; }
    }

    public class Property
    {
        [XmlAttribute("id")]
        public int Id { get; set; }

        public string ClientName => $"rg-col-{Id}";

        [XmlAttribute("alias")]
        public string Alias { get; set; }

        [Obsolete]
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("class")]
        public string CssClass { get; set; }

        [XmlAttribute("style")]
        public string Style { get; set; }

        [XmlAttribute("rgclass")]
        public string RgClass { get; set; }

        [XmlIgnore]
        public int? FlexColumnWidth { get; set; }

        #region
        [XmlAttribute("readonly")]
        public bool Readonly { get; set; }

        [XmlAttribute("disabled")]
        public bool Disabled { get; set; }

        [XmlAttribute("nolabel")]
        public bool NoLabel { get; set; }

        //[XmlAttribute("multiple")]
        //public bool MultipleSelect { get; set; }

        [XmlAttribute("maxlength")]
        public int MaxLength { get; set; } = 0;
        #endregion

        [XmlElement("label")]
        public string Label { get; set; }

        [XmlElement("value")]
        public string OrigValue { get; set; }

        [XmlElement("preElement")]
        public string PreElement { get; set; }

        [XmlArray("dictionary")]
        [XmlArrayItem("item")]
        public List<DictionaryItem> AvailableItems;

        [XmlIgnore]
        public Dictionary<string, string> AvailableDictionary
        {
            get => AvailableItems.ToDictionary(e => e.Key, e => e.Value);
            set => AvailableItems = value.Select(e => new DictionaryItem() { Key = e.Key, Value = e.Value }).ToList();
        }

        [XmlElement("entity")]
        public Entity ForeignEntity { get; set; }

        public bool EmbededGrid => RgClass.Contains("rg-collection");

        [XmlIgnore]
        public RgfEntity EntityDesc { get; set; }

        [XmlIgnore]
        public RgfProperty PropertyDesc { get; set; }
    }

    public class DictionaryItem
    {
        [XmlAttribute("id")]
        public string Key { get; set; }

        [XmlText]
        public string Value { get; set; }
    }

    public class Entity
    {
        [XmlAttribute("entityName")]
        public string EntityName { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlArray("keys")]
        [XmlArrayItem("key")]
        public List<FKey> EntityKeys { get; set; }
    }

    public class FKey
    {
        [XmlAttribute("key")]
        public int Key { get; set; }

        [XmlAttribute("foreign")]
        public int Foreign { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}
