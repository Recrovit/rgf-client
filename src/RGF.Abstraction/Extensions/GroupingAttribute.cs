using System;
using System.Linq;

namespace Recrovit.RecroGridFramework.Abstraction.Extensions;

public class GroupingAttribute : Attribute
{
    public string Group { get; set; }

    public string[] Groups { get; set; }

    public string[] ExcludedGroups { get; set; }

    public bool IncludeAllGroups { get; set; }

    public bool ValidateGroup(string group)
    {
        if (IncludeAllGroups) return true;

        if (ExcludedGroups?.Contains(group) == true) return false;

        return group == Group || Groups?.Contains(group) == true;
    }
}