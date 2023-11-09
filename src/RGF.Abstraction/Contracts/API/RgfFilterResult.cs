using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfFilterResult
{
    public string XmlFilter { get; set; }

    public List<RgfPredefinedFilter> PredefinedFilter { get; set; }

    public bool FilterAdmin { get; set; }

    public bool IsAuthenticated { get; set; }
}
