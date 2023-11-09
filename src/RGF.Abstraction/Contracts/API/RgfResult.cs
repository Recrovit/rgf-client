using Recrovit.RecroGridFramework.Abstraction.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public interface IRgfResult
{
    bool Success { get; }

    RgfMessages Messages { get; }

    object Result { get; }
}

public class RgfResult<TResult> : IRgfResult where TResult : class, new()
{
    public bool Success { get; set; }

    public RgfMessages Messages { get; set; }

    public TResult Result { get; set; } = new();

    object IRgfResult.Result => Result;
}
