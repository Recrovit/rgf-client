using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public class RgfColumnSettings : RgfIdAliasPair
{
    public RgfColumnSettings() { }

    public RgfColumnSettings(IRgfProperty property)
    {
        if (property != null)
        {
            Id = property.Id;
            Alias = property.Alias;
            ColPosOrNull = property.ColPos == 0 ? null : property.ColPos;
            ColWidthOrNull = property.ColWidth == 0 ? null : property.ColWidth;
            Sort = property.Sort == 0 ? null : property.Sort;
        }
    }

    internal RgfColumnSettings(RgfColumnSettings columnSettings) : base(columnSettings)
    {
        if (columnSettings != null)
        {
            ColPosOrNull = columnSettings.ColPosOrNull;
            ColWidthOrNull = columnSettings.ColWidthOrNull;
            Sort = columnSettings.Sort;
        }
    }

    [JsonPropertyName("ColPos")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColPosOrNull { get; set; }

    [JsonPropertyName("ColWidth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ColWidthOrNull { get; set; }

    [JsonPropertyName("Sort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sort { get; set; }

    [JsonPropertyName("External")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RgfExternalColumnSettings ExternalSettings { get; set; }

    public override object Clone() => DeepCopy(this);

    public static RgfColumnSettings DeepCopy(RgfColumnSettings source) => source == null ? null : new RgfColumnSettings(source);
}

public class RgfExternalColumnSettings
{
    public int ExternalId { get; set; }

    public string ExternalPath { get; set; }

    [JsonIgnore]
    public int PropertyId { get; set; }

    [JsonIgnore]
    public string FullPath => string.IsNullOrEmpty(ExternalPath) ? null : $"{ExternalPath}/{ExternalId}";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Aggregate { get; set; }

    public int ColPos { get; set; }
}

public class RgfGridColumnSettings : RgfColumnSettings
{
    public RgfGridColumnSettings(IRgfProperty property, bool external = false) : base(property)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        if (external)
        {
            Property.ColPos = 0;
        }
        ColPosOrNull = Property.ColPos == 0 ? null : Property.ColPos;
        ColWidthOrNull = Property.ColWidth == 0 ? null : Property.ColWidth;
        CssClass = GetCssClass();
    }

    public static IEnumerable<RgfGridColumnSettings> InitColumnSettings(RgfEntity entity, bool external = false)
        => entity.Properties
            .Where(e => e.Readable &&
                (!external || !e.IsDynamic) &&
                e.FormType != PropertyFormType.RecroGrid &&
                e.Options?.GetStringValue("RGO_AutoExternal") == null &&
                e.Options?.GetBoolValue("RGO_AggregationRequired") != true)
            .Select(e => new RgfGridColumnSettings(e, external));

    public static void UpdateColumnSettingsFromProperties(IEnumerable<RgfGridColumnSettings> columnSettings, RgfEntity entityDesc)
    {
        foreach (var prop in entityDesc.Properties)
        {
            var settings = FindById(columnSettings, prop);
            if (settings != null)
            {
                settings.ColPosOrNull = prop.ColPos == 0 ? null : prop.ColPos;
                settings.ColWidthOrNull = prop.ColWidth == 0 ? null : prop.ColWidth;
                settings.CssClass = settings.GetCssClass();
            }
        }
    }

    public static void InitializeExternalSettings(RgfGridColumnSettings settings, RgfGridColumnSettings parent = null, string fullPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.ExternalSettings = new RgfExternalColumnSettings()
        {
            PropertyId = settings.Property.Id,
            ColPos = settings.Property.ColPos
        };

        if (!string.IsNullOrEmpty(fullPath))
        {
            var pos = fullPath.LastIndexOf('/');
            if (pos <= 0 || pos == fullPath.Length - 1 || !int.TryParse(fullPath[(pos + 1)..], out var externalId))
            {
                throw new ArgumentException("Invalid external full path.", nameof(fullPath));
            }

            settings.ExternalSettings.ExternalId = externalId;
            settings.ExternalSettings.ExternalPath = fullPath[..pos];
            return;
        }

        settings.ExternalSettings.ExternalId = settings.Property.Id;
        if (parent != null)
        {
            if (parent.ExternalSettings is null)
            {
                throw new ArgumentException("Parent external settings must be initialized.", nameof(parent));
            }

            settings.PathTitle = $"{parent.PathTitle ?? parent.Property.ColTitle} > {settings.Property.ColTitle}";
            settings.ExternalSettings.ExternalPath = parent.ExternalSettings.ExternalPath;
            if (!string.IsNullOrEmpty(settings.Property.BaseEntityPropertyName))
            {
                settings.ExternalSettings.ExternalPath = string.IsNullOrEmpty(settings.ExternalSettings.ExternalPath)
                    ? settings.Property.BaseEntityPropertyName
                    : $"{settings.ExternalSettings.ExternalPath}.{settings.Property.BaseEntityPropertyName}";
            }
        }
        else
        {
            settings.ExternalSettings.ExternalPath = settings.Property.BaseEntityPropertyName;
        }
    }

    public static RgfGridColumnSettings FindById(IEnumerable<RgfGridColumnSettings> columnSettings, RgfProperty rgfProperty)
    {
        foreach (var settings in columnSettings)
        {
            if (settings.Id == rgfProperty.Id)
            {
                return settings;
            }

            var external = rgfProperty.Options?.GetStringValue("RGO_AutoExternal");
            if (external != null && settings.ExternalSettings?.FullPath == external)
            {
                return settings;
            }

            var result = FindById(settings.RelatedEntityColumnSettings, rgfProperty);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public IRgfProperty Property { get; }

    public RgfEntity BaseEntity { get; set; }

    public IEnumerable<RgfGridColumnSettings> RelatedEntityColumnSettings { get; set; } = Array.Empty<RgfGridColumnSettings>();

    public string CssClass { get; private set; }

    public string PathTitle { get; set; }

    public bool IsExpanded { get; set; }

    private string GetCssClass()
    {
        if (Property.IsKey)
        {
            return "rgf-f-key";
        }
        if (Property.Options?.GetStringValue("RGO_AutoExternal") != null)
        {
            return "rgf-auto-external";
        }
        if (Property.ListType == PropertyListType.RecroGrid)
        {
            return "rgf-f-recrogrid";
        }
        if (Property.Ex?.Contains('E') == true)
        {
            return "rgf-f-entity";
        }
        if (Property.Ex?.Contains('D') == true)
        {
            return "rgf-f-dynamic";
        }
        if (Property.Ex?.Contains('N') == true)
        {
            return "rgf-esql";
        }
        if (Property.Ex?.Contains('B') == true)
        {
            return "rgf-ebase";
        }
        return string.Empty;
    }
}
