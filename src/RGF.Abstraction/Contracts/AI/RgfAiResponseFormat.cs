#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.AI;

// RGF-DOC: rgf.client.abstraction.ai-contracts
/// <summary>Controls the structure of generated user messages without changing their length or business content.</summary>
public enum RgfAiResponseFormat
{
    /// <summary>Plain text with readable line breaks and paragraphs, without Markdown.</summary>
    PlainText,
    /// <summary>Simple Markdown with bullet lists and bold emphasis.</summary>
    Minimal,
    /// <summary>Simple Markdown plus headings, numbered lists and links.</summary>
    Normal,
    /// <summary>Fuller Markdown, including tables and code blocks when useful.</summary>
    Maximum
}
