using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Recrovit.RecroGridFramework.Abstraction.Models;

public interface IRgfProgressHub
{
    Task ReceiveProgress(IRgfProgressArgs args);
}

public enum RgfProgressType
{
    Default = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Success = 4
}

public interface IRgfProgressArgs
{
    RgfProgressType ProgressType { get; }

    string Message { get; }

    int? TotalIterations { get; }

    int? CurrentIteration { get; }

    decimal? Percentage { get; }

    Dictionary<string, object> CustomData { get; set; }
}

public class RgfProgressArgs : IRgfProgressArgs
{
    public RgfProgressType ProgressType { get; set; } = RgfProgressType.Default;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalIterations { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CurrentIteration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Percentage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object> CustomData { get; set; }

    public RgfProgressArgs() { }

    public RgfProgressArgs(string message)
    {
        Message = message;
    }

    public RgfProgressArgs(decimal percentage, string message = null) : this(message)
    {
        Percentage = percentage;
    }

    public RgfProgressArgs(int totalIterations, int currentIteration, string message = null) : this(message)
    {
        CurrentIteration = currentIteration;
        TotalIterations = totalIterations;
    }
}