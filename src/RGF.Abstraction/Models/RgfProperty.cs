using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public enum PropertyFormType
{
    [EnumMember(Value = "invalid")]
    Invalid = 0,
    [EnumMember(Value = "textbox")]
    TextBox = 1,
    [EnumMember(Value = "textboxmultiline")]
    TextBoxMultiLine = 2,
    [EnumMember(Value = "checkbox")]
    CheckBox = 3,
    [EnumMember(Value = "dropdown")]
    DropDown = 4,
    [EnumMember(Value = "date")]
    Date = 5,
    [EnumMember(Value = "datetime")]
    DateTime = 6,
    [EnumMember(Value = "recrogrid")]
    RecroGrid = 7,
    [EnumMember(Value = "entity")]
    Entity = 8,
    [EnumMember(Value = "statictext")]
    StaticText = 10,
    [EnumMember(Value = "imageindb")]
    ImageInDB = 11,
    [EnumMember(Value = "recrodict")]
    RecroDict = 13,
    [EnumMember(Value = "htmleditor")]
    HtmlEditor = 14,
    [EnumMember(Value = "listbox")]
    ListBox = 15,
    [EnumMember(Value = "custom")]
    Custom = 16,
}
public enum PropertyListType
{
    [EnumMember(Value = "string")]
    String = 0,
    [EnumMember(Value = "numeric")]
    Numeric = 1,
    [EnumMember(Value = "date")]
    Date = 2,
    [EnumMember(Value = "html")]
    Html = 3,
    [EnumMember(Value = "image")]
    Image = 4,
    [EnumMember(Value = "recrogrid")]
    RecroGrid = 5
}

public enum ClientDataType
{
    Undefined = 0,
    String = 1,
    Integer = 2,
    Decimal = 3,
    Double = 4,
    DateTime = 5,
    Boolean = 7,
}

public static class EnumExtension
{
    public static bool IsNumeric(this ClientDataType data)
    {
        switch (data)
        {
            case ClientDataType.Integer:
            case ClientDataType.Decimal:
            case ClientDataType.Double:
                return true;
        }
        return false;
    }
}
public interface IRgfProperty
{
    string Alias { get; set; }

    string ClientName { get; set; }

    int ColPos { get; set; }

    string ColTitle { get; set; }

    int ColWidth { get; set; }

    bool Editable { get; set; }

    string Ex { get; set; }

    ClientDataType ClientDataType { get; }

    PropertyFormType FormType { get; set; }

    int Id { get; set; }

    bool IsKey { get; set; }

    PropertyListType ListType { get; set; }

    Dictionary<string, object> Options { get; set; }

    bool Orderable { get; set; }

    bool Readable { get; set; }

    int Sort { get; set; }
}

public class RgfProperty : IRgfProperty
{
    public int Id { get; set; }

    public string ClientName { get; set; }

    public string Alias { get; set; }

    public string ColTitle { get; set; }

    public PropertyListType ListType { get; set; }

    public int ColPos { get; set; }

    public int ColWidth { get; set; }

    public PropertyFormType FormType { get; set; }

    public int FormTab { get; set; }

    public int FormGroup { get; set; }

    public int FormPos { get; set; }

    public int Sort { get; set; }

    public bool IsKey { get; set; }

    public bool Readable { get; set; }

    public bool Editable { get; set; }

    public bool Orderable { get; set; }

    public string Ex { get; set; }

    public Dictionary<string, object> Options { get; set; }

    [JsonIgnore]
    public int? MaxLength => Options?.TryGetIntValue("RGO_MaxLength");

    [JsonIgnore]
    public bool PasswordType => Options?.GetBoolValue("RGO_Password") ?? false;

    [JsonIgnore]
    public bool Nullable => Options?.GetBoolValue("RGO_Nullable") ?? false;

    [JsonIgnore]
    public bool Required => !Nullable;

    public ClientDataType ClientDataType
    {
        get
        {
            switch (FormType)
            {
                case PropertyFormType.Invalid:
                case PropertyFormType.RecroGrid:
                case PropertyFormType.Entity:
                case PropertyFormType.Custom:
                    return ClientDataType.Undefined;

                case PropertyFormType.Date:
                case PropertyFormType.DateTime:
                    return ClientDataType.DateTime;

                case PropertyFormType.DropDown:
                case PropertyFormType.ListBox:
                    return ClientDataType.String;//kliens oldalon mindig string

                case PropertyFormType.CheckBox:
                    if (Options?.GetBoolValue("RGO_Nullable") != true)
                    {
                        return ClientDataType.Boolean;
                    }
                    return ClientDataType.String;

                default:
                    if (ListType == PropertyListType.Numeric)
                    {
                        if (!IsKey)
                        {
                            /*TODO: ClientDataType => Integer, Decimal, Double ?
                            if (this.Options.GetBoolValue(?))
                            {
                                return ClientDataType.Decimal;
                            }
                            if (this.Options.GetBoolValue(?))*/
                            {
                                return ClientDataType.Double;
                            }
                        }
                        return ClientDataType.Integer;
                    }
                    return ClientDataType.String;
            }
        }
    }
}

public class GridColumnSettings : RgfColumnSettings
{
    public GridColumnSettings(RgfProperty property)
    {
        Property = property;
        Id = property.Id;
        ColPos = property.ColPos == 0 ? null : property.ColPos;
        ColWidth = property.ColWidth == 0 ? null : property.ColWidth;
        CssClass = GetCssClass();
    }

    public RgfProperty Property { get; }

    public string CssClass { get; }

    private string GetCssClass()
    {
        RgfProperty property = this.Property;
        string cssClass = string.Empty;
        if (property.IsKey)
        {
            cssClass = "rgf-f-key";
        }
        else if (property.ListType == PropertyListType.RecroGrid)
        {
            cssClass = "rgf-f-recrogrid";
        }
        else if (property.Ex.IndexOf('E') != -1)
        {
            cssClass = "rgf-f-entity";
        }
        else if (property.Ex.IndexOf('D') != -1)
        {
            cssClass = "rgf-f-dynamic";
        }
        else if (property.Ex.IndexOf('B') != -1)
        {
            cssClass = "rgf-ebase";
        }
        else if (property.Ex.IndexOf('N') != -1)
        {
            cssClass = "rgf-esql";
        }
        return cssClass;
    }
}
