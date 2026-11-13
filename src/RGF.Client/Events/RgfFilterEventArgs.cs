namespace Recrovit.RecroGridFramework.Client.Events;

public enum RgfFilterEventKind
{
    /// <summary>A Recroby filter and its editor settings have been applied.</summary>
    AiFilterApplied = 1
}

public class RgfFilterEventArgs(RgfFilterEventKind eventKind) : EventArgs
{
    public RgfFilterEventKind EventKind { get; } = eventKind;
}
