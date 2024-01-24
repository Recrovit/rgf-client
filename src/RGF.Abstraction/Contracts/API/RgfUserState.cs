using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfUserState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsValid { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsAdmin { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Language { get; set; }
}
