using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfMarkdownViewerTests
{
    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        return context;
    }

    [Fact]
    public void FormatsCommonMarkAndPipeTables()
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text,
            "# Heading\n\n**bold** *emphasis*\n\n- item\n\n```csharp\n<x>\n```\n\n| A | B |\n|---|---|\n| 1 | 2 |"));
        Assert.Equal("Heading", cut.Find("h1").TextContent);
        Assert.Equal("bold", cut.Find("strong").TextContent);
        Assert.Equal("emphasis", cut.Find("em").TextContent);
        Assert.Single(cut.FindAll("li"));
        Assert.Contains("<x>", cut.Find("pre code").TextContent);
        Assert.Equal(2, cut.FindAll("td").Count);
        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void TogglePreservesOriginalTextAndSurvivesUnchangedParameters()
    {
        const string text = "**bold**\n\n<script>alert(1)</script>";
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text, text).Add(c => c.ShowToggle, true));
        cut.Find("button").Click();
        Assert.Equal(text, cut.Find(".rgf-markdown-plain").TextContent);
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
        Assert.Equal("View Markdown", cut.Find("button").GetAttribute("title"));
        Assert.Empty(cut.FindAll("script, strong"));
        cut.Render(p => p.Add(c => c.Text, text).Add(c => c.RenderMarkdown, true));
        Assert.Single(cut.FindAll(".rgf-markdown-plain"));
        cut.Render(p => p.Add(c => c.Text, "**changed**"));
        Assert.Equal("changed", cut.Find("strong").TextContent);
        cut.Find("button").Click();
        Assert.Equal("**changed**", cut.Find(".rgf-markdown-plain").TextContent);
        cut.Find("button").Click();
        Assert.Single(cut.FindAll("strong"));
        cut.Render(p => p.Add(c => c.RenderMarkdown, false));
        Assert.Single(cut.FindAll(".rgf-markdown-plain"));
        cut.Find("button").Click();
        cut.Render(p => p.Add(c => c.Text, "new\n\ntext"));
        Assert.Equal("new\n\ntext", cut.Find(".rgf-markdown-plain").TextContent);
        Assert.Equal("new\n\ntext", cut.Instance.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputCanToggle(string? text)
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text, text).Add(c => c.ShowToggle, true));
        Assert.Empty(cut.Find(".rgf-markdown-content").TextContent);
        cut.Find("button").Click();
        Assert.Empty(cut.Find(".rgf-markdown-plain").TextContent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToggleStaysRenderedWithAccessibleState(bool markdown)
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text, "**text**")
            .Add(c => c.RenderMarkdown, markdown).Add(c => c.ShowToggle, true));
        void AssertToggle(bool isMarkdown)
        {
            var button = Assert.Single(cut.FindAll("button"));
            var label = isMarkdown ? "View plain text" : "View Markdown";
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.Equal(label, button.GetAttribute("title"));
            Assert.Equal(label, button.GetAttribute("aria-label"));
            Assert.Equal(isMarkdown ? "true" : "false", button.GetAttribute("aria-pressed"));
            Assert.Contains("rgf-markdown-toggle", button.ClassList);
            Assert.Equal("true", button.QuerySelector("i")!.GetAttribute("aria-hidden"));
            Assert.Contains(isMarkdown ? "bi-file-text" : "bi-markdown", button.QuerySelector("i")!.ClassList);
        }
        AssertToggle(markdown);
        cut.Find("button").Click();
        AssertToggle(!markdown);
        cut.Render();
        AssertToggle(!markdown);
        cut.Render(p => p.Add(c => c.ShowToggle, false));
        Assert.Empty(cut.FindAll("button"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("jav&#x61;script:alert(1)")]
    [InlineData("java%73cript:alert(1)")]
    [InlineData("java%2573cript:alert(1)")]
    [InlineData("java%0ascript:alert(1)")]
    [InlineData("data:text/html,evil")]
    [InlineData("file:///C:/secret")]
    [InlineData("//example.test/path")]
    [InlineData("/relative")]
    [InlineData("mailto:user@example.test")]
    [InlineData("vbscript:evil")]
    [InlineData("https://example.test/%0d%0aevil")]
    [InlineData("https://example.test/%250D%250Aevil")]
    [InlineData("https://example.test/%00evil")]
    [InlineData("https://example.test/%09evil")]
    [InlineData("https://example.test/%1Fevil")]
    [InlineData("https://example.test/%7Fevil")]
    [InlineData("https://example.test/%5Cevil")]
    [InlineData("https://example.test/%255Cevil")]
    [InlineData("https://example.test/&#x0D;evil")]
    [InlineData("https://example.test/\u0001evil")]
    [InlineData("https://example.test/\\evil")]
    [InlineData("%6Aavascript:evil")]
    [InlineData("data%3Atext/html,evil")]
    [InlineData("vb%73cript:evil")]
    public void UnsafeLinksNeverBecomeClickable(string url)
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text, $"[label](<{url}>) <{url}>"));
        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("label", cut.Markup);
    }

    [Fact]
    public void OnlySafeLinksAndKnownMarkupAreEmitted()
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text,
            "<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>\n\n![alternative](https://example.test/image.png)\n\n[link](HTTPS://example.test/path?x=1&y=2) <https://example.test/auto> <user@example.test>"));
        Assert.Empty(cut.FindAll("script, img, iframe, style"));
        Assert.Contains("alternative", cut.Find(".rgf-markdown-content").TextContent);
        Assert.Equal(2, cut.FindAll("a").Count);
        Assert.All(cut.FindAll("a"), a =>
        {
            Assert.Equal("https", new Uri(a.GetAttribute("href")!).Scheme);
            Assert.Equal("example.test", new Uri(a.GetAttribute("href")!).Host);
            Assert.Single(a.Attributes);
        });
    }

    [Theory]
    [InlineData("HTTPS://example.test/a%20b")]
    [InlineData("http://example.test/path?x=1&y=2")]
    [InlineData("https://example.test/search?q=a%26b")]
    [InlineData("https://example.test/search?q=a%2526b")]
    [InlineData("https://example.test/a%2Fb")]
    [InlineData("https://example.test/search?q=a%3Db%23c")]
    [InlineData("https://example.test/path#part%26a%2Fb%2523")]
    public void SafeLinksAndAutolinksPreserveEscapedUrlMeaning(string source)
    {
        using var context = Context();
        var cut = context.Render<RgfMarkdownViewer>(p => p.Add(c => c.Text, $"[label](<{source}>) <{source}>"));
        var links = cut.FindAll("a");
        Assert.Equal(2, links.Count);
        foreach (var link in links)
        {
            var href = link.GetAttribute("href")!;
            Assert.Equal(new Uri(source), new Uri(href));
            Assert.Equal(new Uri(source).GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped),
                new Uri(href).GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped));
        }
        if (source.Contains('&')) Assert.Contains($"href=\"{source.Replace("&", "&amp;")}\"", cut.Markup);
    }
}
