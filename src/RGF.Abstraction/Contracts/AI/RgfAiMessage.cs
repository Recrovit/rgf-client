#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>A provider-independent message in an AI conversation.</summary>
public class RgfAiMessage
{
    public RgfAiMessageRole Role { get; set; }

    public virtual string Message { get; set; } = string.Empty;

    /// <summary>Time the message was created, including its UTC offset.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
