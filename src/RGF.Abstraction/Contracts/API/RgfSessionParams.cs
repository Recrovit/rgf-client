using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

/// <summary>Shared backend session, grid, language and progress-connection identifiers carried by requests.</summary>
public class RgfSessionParams
{
    /// <summary>Creates session parameters with no assigned identifiers.</summary>
    public RgfSessionParams() { }

    /// <summary>Copies the shared session values when a source is supplied.</summary>
    /// <param name="param">Source session, or null to leave all values unset.</param>
    public RgfSessionParams(RgfSessionParams param)
    {
        if (param != null)
        {
            SessionId = param.SessionId;
            GridId = param.GridId;
            Language = param.Language;
            ClientConnectionId = param.ClientConnectionId;
        }
    }

    /// <summary>Backend session identifier; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string SessionId { get; set; }

    /// <summary>Backend grid instance identifier; omitted from JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string GridId { get; set; }

    /// <summary>Requested backend language identifier; omitted when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Language { get; set; }

    /// <summary>Progress connection identifier used to associate backend notifications with the client.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string ClientConnectionId { get; set; }
}