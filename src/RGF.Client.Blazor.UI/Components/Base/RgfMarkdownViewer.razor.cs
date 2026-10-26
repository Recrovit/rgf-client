using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

// RGF-DOC: rgf.client.blazor-ui.markdown-viewer
/// <summary>Displays untrusted Markdown or its original text, with an optional local view toggle.</summary>
public partial class RgfMarkdownViewer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    private bool _initialized;
    private string? _previousText;
    private bool _previousRenderMarkdown;
    private bool _renderMarkdown;
    private string _html = string.Empty;

    /// <summary>Original text to display; never modified by the viewer.</summary>
    [Parameter] public string? Text { get; set; }
    /// <summary>Initial view, reapplied only when this value or <see cref="Text"/> changes.</summary>
    [Parameter] public bool RenderMarkdown { get; set; } = true;
    /// <summary>Shows an accessible button for switching the local view.</summary>
    [Parameter] public bool ShowToggle { get; set; }

    private string ToggleLabel => _renderMarkdown ? "View plain text" : "View Markdown";

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (!_initialized || !string.Equals(Text, _previousText, StringComparison.Ordinal))
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var renderer = new HtmlRenderer(writer);
            Pipeline.Setup(renderer);
            renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new SafeLinkRenderer());
            renderer.ObjectRenderers.Replace<AutolinkInlineRenderer>(new SafeAutolinkRenderer());
            renderer.Render(Markdown.Parse(Text ?? string.Empty, Pipeline));
            _html = writer.ToString();
        }
        if (!_initialized || Text != _previousText || RenderMarkdown != _previousRenderMarkdown)
            _renderMarkdown = RenderMarkdown;
        _previousText = Text;
        _previousRenderMarkdown = RenderMarkdown;
        _initialized = true;
    }

    // Decode only a separate inspection value; preserve the original URL's escaped delimiters.
    private static string? SafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var original)
                || !(original.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                    || original.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                || string.IsNullOrEmpty(original.Host) || original.HostNameType == UriHostNameType.Unknown)
                return null;

            var inspection = value;
            for (var pass = 0; pass < 8; pass++)
            {
                if (inspection != inspection.Trim() || inspection.Any(char.IsControl) || inspection.Contains('\\'))
                    return null;
                var decoded = WebUtility.HtmlDecode(Uri.UnescapeDataString(inspection));
                if (decoded == inspection)
                {
                    return Uri.TryCreate(inspection, UriKind.Absolute, out var uri)
                        && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                            || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                        && !string.IsNullOrEmpty(uri.Host) ? value : null;
                }
                inspection = decoded;
            }
        }
        catch (ArgumentException) { }
        catch (UriFormatException) { }
        return null;
    }

    private sealed class SafeLinkRenderer : HtmlObjectRenderer<LinkInline>
    {
        protected override void Write(HtmlRenderer renderer, LinkInline link)
        {
            var url = link.IsImage ? null : SafeUrl(link.Url);
            var clickable = renderer.EnableHtmlForInline && url is not null;
            if (clickable) renderer.Write("<a href=\"").WriteEscape(url).Write("\">");
            renderer.WriteChildren(link);
            if (clickable) renderer.Write("</a>");
        }
    }

    private sealed class SafeAutolinkRenderer : HtmlObjectRenderer<AutolinkInline>
    {
        protected override void Write(HtmlRenderer renderer, AutolinkInline link)
        {
            var url = link.IsEmail ? null : SafeUrl(link.Url);
            var clickable = renderer.EnableHtmlForInline && url is not null;
            if (clickable) renderer.Write("<a href=\"").WriteEscape(url).Write("\">");
            renderer.WriteEscape(link.Url);
            if (clickable) renderer.Write("</a>");
        }
    }
}
