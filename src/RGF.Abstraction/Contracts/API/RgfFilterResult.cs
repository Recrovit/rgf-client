using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Collections.Generic;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfFilterResult
{
    public string XmlFilter { get; set; }

    public List<RgfFilterSettings> FilterSettings { get; set; }
}