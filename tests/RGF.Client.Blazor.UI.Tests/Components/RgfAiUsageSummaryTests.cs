using Bunit;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfAiUsageSummaryTests
{
    [Fact]
    public async Task PopoversHaveStableUniqueIdsAccessibleButtonsAndModuleDisposal()
    {
        await using var context = Context();
        var module = context.JSInterop.SetupModule(_ => true);
        module.SetupVoid("sync", _ => true).SetVoidResult();
        module.SetupVoid("dispose", _ => true).SetVoidResult();
        var response = new RgfAiResponse { CreditUsage = new() { Tokens = new() }, CreditState = new() };
        var first = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        var second = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        var ids = first.FindAll("[popover]").Select(e => e.Id).ToArray();
        Assert.DoesNotContain(second.FindAll("[popover]"), e => ids.Contains(e.Id));
        foreach (var button in first.FindAll("button"))
        {
            Assert.Equal("button", button.GetAttribute("type"));
            Assert.Equal("dialog", button.GetAttribute("aria-haspopup"));
            Assert.Equal(button.GetAttribute("popovertarget"), button.GetAttribute("aria-controls"));
            Assert.Equal("auto", first.Find("#" + button.GetAttribute("popovertarget")).GetAttribute("popover"));
        }
        first.Render();
        Assert.Equal(ids, first.FindAll("[popover]").Select(e => e.Id));
        await context.DisposeAsync();
        Assert.Equal(2, module.Invocations["dispose"].Count);
        Assert.All(module.Invocations["dispose"], invocation => Assert.IsType<string>(Assert.Single(invocation.Arguments)));
    }
    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    [Theory]
    [InlineData(3.2, "3.2 s")]
    [InlineData(0, "0 s")]
    [InlineData(72, "1m 12s")]
    [InlineData(3600, "60m 0s")]
    public void ExecutionTimeIsReadableAndDoesNotOpenAPanel(double seconds, string expected)
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditUsage = new() { ExecutionTime = TimeSpan.FromSeconds(seconds) } }));
        Assert.Equal(expected.Replace('.', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]), cut.Find(".rgf-ai-usage-duration").TextContent.Trim());
        Assert.Empty(cut.FindAll(".rgf-ai-usage-duration[popovertarget], .bi-info-circle"));
    }

    [Fact]
    public void EveryReportedTokenFieldAndUnknownIdentityIsDisplayedWithoutFallback()
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditUsage = new() {
            Tokens = new() { InputTokens = 0, OutputTokens = 7, TotalTokens = 99, CachedInputTokens = 3, ReasoningTokens = 2 },
            Models = [new() { Tokens = new() }]
        } }));
        Assert.Equal(new[] { "0", "7", "99", "3", "2" },
            cut.FindAll("[id$=-tokens] dd").Select(e => e.TextContent));
        Assert.Equal(new[] { "Unknown", "Unknown" }, cut.FindAll("[id$=-models] dd").Select(e => e.TextContent));

        var usage = cut.Instance.Response.CreditUsage!;
        var model = new RgfAiModelCreditUsage { ProviderId = "openai", ModelId = "gpt-6-luna", Tokens = new() {
            InputTokens = 0, OutputTokens = 7, TotalTokens = 99, CachedInputTokens = 3, ReasoningTokens = 2
        } };
        usage.Models = [model];
        cut.Render();
        var panel = cut.Find("[id$=-tokens]");
        Assert.Single(panel.QuerySelectorAll("dl"));
        Assert.DoesNotContain("openai / gpt-6-luna", panel.TextContent);
        Assert.DoesNotContain("Request tokens", panel.TextContent);
        Assert.Equal(new[] { "0", "7", "99", "3", "2" }, panel.QuerySelectorAll("dd").Select(e => e.TextContent));

        model.Tokens.ReasoningTokens = null;
        cut.Render();
        Assert.Single(cut.FindAll("[id$=-tokens] dl"));
        Assert.Equal("2", cut.FindAll("[id$=-tokens] dd")[4].TextContent);

        model.Tokens.ReasoningTokens = 2;
        usage.Models = [model, new() { ProviderId = "other", ModelId = "other-model", Tokens = model.Tokens }];
        cut.Render();
        Assert.Single(cut.FindAll("[id$=-tokens] dl"));
        Assert.DoesNotContain("other-model", cut.Find("[id$=-tokens]").TextContent);
        usage.Tokens!.TotalTokens = null;
        cut.Render();
        Assert.Equal("Unknown", cut.FindAll("[id$=-tokens] dd")[2].TextContent);
        usage.Tokens = null;
        cut.Render();
        Assert.Empty(cut.FindAll(".bi-bar-chart, [id$=-tokens]"));
    }

    [Fact]
    public void NoCreditDataRendersNothingEvenWithLegacyUsage()
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { Usage = new() }));
        Assert.Empty(cut.Markup.Trim());
    }

    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData("0", "0")]
    [InlineData("0.00125000", "0.00125")]
    [InlineData("1.123456789", "1.12345679")]
    public void CreditsDistinguishUnknownZeroAndTrimToEightPlaces(string? source, string expected)
    {
        using var context = Context();
        var value = source is null ? (decimal?)null : decimal.Parse(source, System.Globalization.CultureInfo.InvariantCulture);
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditUsage = new() { ChargedCredit = value } }));
        Assert.Contains(expected.Replace('.', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]), cut.Find(".rgf-ai-usage-item").TextContent);
    }

    [Fact]
    public void UsesDistinctServerModelsAndNeverSumsUnknownCostsOrTokens()
    {
        using var context = Context();
        var response = new RgfAiResponse { CreditUsage = new() { ExecutionTime = TimeSpan.FromSeconds(72), Models = [
            new() { ProviderId = "p", ModelId = "actual", ChargedCredit = 2 },
            new() { ProviderId = "p", ModelId = "actual", ChargedCredit = 3, Tokens = new() { InputTokens = 0, OutputTokens = 7 } }
        ] } };
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        Assert.Contains("actual", cut.Find(".rgf-ai-usage-item").TextContent);
        Assert.Contains("1m 12s", cut.Markup);
        Assert.Equal("Unknown", cut.Find("[id$=-cost] dd").TextContent);
        Assert.Empty(cut.FindAll(".bi-bar-chart, [id$=-tokens]"));
        response.CreditUsage.Models = [new() { ProviderId = "p", ModelId = "a" }, new() { ProviderId = "q", ModelId = "a" }];
        cut.Render();
        Assert.Contains("2 models", cut.Markup);
        Assert.Empty(cut.FindAll(".bi-bar-chart"));
    }

    [Theory]
    [InlineData(AiCreditMode.Disabled, "Disabled", "Credit access is disabled.")]
    [InlineData(AiCreditMode.Unlimited, "Unlimited", "Unlimited access.")]
    [InlineData(AiCreditMode.Prepaid, "Unknown", "Expired")]
    [InlineData(AiCreditMode.Periodic, "Limits", "Unknown")]
    public void MissingStateDetailsRespectMode(AiCreditMode mode, string label, string detail)
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditState = new() { Mode = mode } }));
        Assert.Contains(label, cut.Find(".rgf-ai-usage-item").TextContent);
        var button = cut.Find("button[popovertarget$=-credit]");
        var icon = button.QuerySelector("i")!;
        Assert.Equal("bi bi-piggy-bank", icon.ClassName);
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Empty(button.QuerySelectorAll(".bi-wallet2, [class*=fill]"));
        Assert.Contains(detail, cut.Find("[role=dialog]").TextContent);
        Assert.Contains("Snapshot at response completion", cut.Markup);
    }

    [Fact]
    public void PrepaidAndIndependentPeriodsPreserveExpiryNegativeBalanceAndUtcSource()
    {
        using var context = Context();
        var response = new RgfAiResponse { CreditState = new() { Mode = AiCreditMode.Prepaid,
            Prepaid = new(-2, new DateOnly(2026, 10, 13), true, DateTime.UtcNow) } };
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        Assert.Contains("-2", cut.Markup);
        Assert.Contains("2026-10-13", cut.Markup);
        Assert.Contains("Yes", cut.Markup);
        response.CreditState = new() { Mode = AiCreditMode.Periodic, Periods = [
            new(AiCreditPeriodType.Daily, 1, 3, new DateTime(2026, 10, 14), DateTime.UtcNow),
            new(AiCreditPeriodType.Monthly, 100, 3, new DateTime(2026, 11, 1), DateTime.UtcNow) ] };
        cut.Render();
        Assert.Contains("Limits", cut.Find(".rgf-ai-usage-item").TextContent);
        Assert.Equal(2, cut.FindAll("time").Count);
        Assert.EndsWith("Z", cut.Find("time").GetAttribute("datetime"));
        Assert.Contains("-2", cut.Find("[role=dialog]").TextContent);
        Assert.Contains("97", cut.Find("[role=dialog]").TextContent);
    }
}
