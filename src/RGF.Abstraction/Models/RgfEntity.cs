using Recrovit.RecroGridFramework.Abstraction.Extensions;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfEntity
{
    public int EntityId { get; set; }

    public string EntityName { get; set; }

    public string EntityVersion { get; set; }

    [JsonIgnore]
    public string NameVersion => $"{EntityName}_{EntityVersion}";

    public string Title { get; set; }

    public string MenuTitle { get; set; }

    public string CRUD { get; set; }

    public RgfPermissions Permissions { get; set; }

    public bool IsRecroTrackReadable { get; set; }

    public List<RgfProperty> Properties { get; set; } = new();

    public Dictionary<string, object> Options { get; set; } = new();

    public List<string> StylesheetsReferences { get; set; }

    [JsonIgnore]
    public int ItemsPerPage => (int)Options.GetLongValue("RGO_ItemsPerPage", 15);

    [JsonIgnore]
    public int Preload => (int)Options.GetLongValue("RGO_Preload", ItemsPerPage * 2);

    [JsonIgnore]
    public IEnumerable<RgfProperty> SortedVisibleColumns => Properties.Where(e => e.Readable && e.ColPos > 0 && e.ListType != PropertyListType.RecroGrid).OrderBy(e => e.ColPos).ThenBy(e => e.ColTitle);

    [JsonIgnore]
    public IEnumerable<RgfProperty> SortColumns => Properties.Where(e => e.Sort != 0).OrderBy(e => Math.Abs(e.Sort));
}

public class RgfEntityKey : IEquatable<RgfEntityKey>
{
    public RgfDynamicDictionary Keys { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Signature { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Keys?.Any() != true;

    public bool Equals(RgfEntityKey other)
    {
        if (this.IsEmpty && other?.IsEmpty != false)
        {
            return true;
        }
        return Signature.Equals(other.Signature) && this.Keys.Equals(other.Keys);
    }
}
