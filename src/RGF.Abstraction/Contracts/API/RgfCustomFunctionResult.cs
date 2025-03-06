using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfCustomFunctionResult
{
    public object Results { get; set; }

    public bool RefreshGrid { get; set; }

    public bool RefreshRow{ get; set; }

    public RgfDynamicDictionary Row { get; set; }

    public bool StartedInBackground { get; set; }
}