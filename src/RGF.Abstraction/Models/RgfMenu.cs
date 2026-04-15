using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Models
{
    public enum RgfMenuType
    {
        Invalid = 0,

        /// <summary>
        /// Legacy => Global Function
        /// </summary>
        Function = 1,

        /// <summary>
        /// Legacy => Function for record
        /// </summary>
        FunctionForRec = 2,

        /// <summary>
        /// Legacy => (Submenu = 3) 
        /// </summary>
        Menu = 3,

        Divider = 4,

        /// <summary>
        /// Legacy => Disabled
        /// </summary>
        Disabled = 5,

        /// <summary>
        /// Legacy => (URL = 6) Direct link menu item
        /// </summary>
        ActionLink = 6,

        Custom = 11
    }

    public class RgfMenu
    {
        public RgfMenu() : this(RgfMenuType.Invalid) { }

        public RgfMenu(RgfMenuType menuType, string title = null, string command = null)
        {
            MenuType = menuType;
            Title = title;
            Command = command;
            NestedMenu = new();
        }

        public RgfMenuType MenuType { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RgfMenu> NestedMenu { get; set; }

        public string Title { get; set; }

        public string Command { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string CssClass { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Disabled { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Scope { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Icon { get; set; }

        [JsonIgnore]
        [Obsolete]
        public bool Separator => MenuType == RgfMenuType.Divider;

        [JsonIgnore]
        [Obsolete]
        public List<RgfMenu> NestedMenuOrNull => NestedMenu?.Any() == true ? NestedMenu : null;

        [JsonIgnore]
        public bool Enabled => !Disabled;
    }
}