using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfUserState
{
    public bool IsValid { get; set; }

    public bool IsAdmin { get; set; }

    public string Language { get; set; }
}
