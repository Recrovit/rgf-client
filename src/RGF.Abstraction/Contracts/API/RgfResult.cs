using Recrovit.RecroGridFramework.Abstraction.Models;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public interface IRgfResult
{
    bool Success { get; }

    RgfCoreMessages Messages { get; }

    object Result { get; }
}

public class RgfResult<TResult> : IRgfResult where TResult : class, new()
{
    public bool Success { get; set; }

    public RgfCoreMessages Messages { get; set; }

    public TResult Result { get; set; } = new();

    object IRgfResult.Result => Result;
}