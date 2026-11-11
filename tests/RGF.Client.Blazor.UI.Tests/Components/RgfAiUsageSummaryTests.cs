using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Bunit;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI.Server;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfAiUsageSummaryTests
{
    [Fact]
    public void CallCountAliasesAndSentEffortAreVisibleWithoutTokenData()
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new()
        {
            CreditUsage = new() { LlmCallCount = 3, ReasoningEffort = "high",
                Models = [new() { ProviderId = "provider-alias", ModelId = "model-alias" }] }
        }));
        Assert.Equal("3", cut.Find("[id$=-tokens] dd").TextContent);
        Assert.Equal("LLM calls", cut.Find("[id$=-tokens] dt").TextContent);
        Assert.Empty(cut.FindAll("[aria-label='LLM calls']"));
        Assert.Equal(new[] { "provider-alias", "model-alias", "high" },
            cut.FindAll("[id$=-models] dd").Select(element => element.TextContent));
        Assert.Single(cut.FindAll("[id$=-tokens] dd"));
    }

    [Theory]
    [InlineData(true, 1250L, 3, "1250", "Total tokens")]
    [InlineData(false, null, 3, "3", "LLM calls")]
    [InlineData(true, null, 2, "2", "LLM calls")]
    [InlineData(false, null, 0, "0", "LLM calls")]
    [InlineData(true, null, 0, "0", "LLM calls")]
    [InlineData(true, 0L, 3, "0", "Total tokens")]
    public void UsageButtonShowsKnownTotalOrLogicalCallCount(bool hasTokens, long? totalTokens, int callCount, string value, string label)
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new()
        {
            CreditUsage = new()
            {
                LlmCallCount = callCount,
                Tokens = hasTokens ? new() { TotalTokens = totalTokens, InputTokens = 7 } : null
            }
        }));

        var button = cut.Find("button[popovertarget$=-tokens]");
        Assert.Equal(value, button.TextContent.Trim());
        Assert.False(button.HasAttribute("title"));
        Assert.False(button.HasAttribute("data-rgf-usage-tooltip"));
        Assert.Equal($"{label}: {value}", button.GetAttribute("aria-label"));
        Assert.Equal("bi bi-bar-chart", button.QuerySelector("i")!.ClassName);

        var panel = cut.Find("[id$=-tokens]");
        Assert.Equal("LLM calls", panel.QuerySelector("dt")!.TextContent);
        Assert.Equal(callCount.ToString(), panel.QuerySelector("dd")!.TextContent);
        Assert.Equal(hasTokens ? 2 : 1, panel.QuerySelectorAll("dl").Length);
        if (hasTokens)
        {
            Assert.Equal("Aggregated token statistics", panel.QuerySelector("strong")!.TextContent);
            Assert.Equal(new[] { callCount.ToString(), "7", "Unknown", totalTokens?.ToString() ?? "Unknown", "Unknown", "Unknown" },
                panel.QuerySelectorAll("dd").Select(element => element.TextContent));
        }
        else
        {
            Assert.Empty(panel.QuerySelectorAll("strong"));
            Assert.Single(panel.QuerySelectorAll("dd"));
        }
    }

    [Fact]
    public void CreditStateWithoutCreditUsageHasNoUsageButton()
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditState = new() }));
        Assert.Empty(cut.FindAll("button[popovertarget$=-tokens], [id$=-tokens]"));
        Assert.Single(cut.FindAll("button[popovertarget$=-credit]"));
    }

    [Fact]
    public async Task PopoversHaveStableUniqueIdsAccessibleButtonsAndInstanceDisposal()
    {
        await using var context = Context();
        context.JSInterop.SetupVoid("Recrovit.RGF.Blazor.UI.AI.Usage.sync", _ => true).SetVoidResult();
        context.JSInterop.SetupVoid("Recrovit.RGF.Blazor.UI.AI.Usage.dispose", _ => true).SetVoidResult();
        var languageEventLog = new ListLogger();
        var security = new FakeRecroSecService(languageEventLog);
        context.Services.AddSingleton<IRecroSecService>(security);
        var dictionary = (FakeDashboardRecroDictService)context.Services.GetRequiredService<IRecroDictService>();
        dictionary.HungarianRecrobyUi["LlmCalls"] = "LLM-hívások";
        var response = new RgfAiResponse { CreditUsage = new() { LlmCallCount = 3, Tokens = new() }, CreditState = new() };
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
        await dictionary.InitializeAsync("hun");
        await security.LanguageChangedEvent.InvokeAsync(new("hun"));
        Assert.Equal(2, languageEventLog.Entries.Count);
        Assert.Equal(ids, first.FindAll("[popover]").Select(e => e.Id));
        var usage = first.Find("button[popovertarget$=-tokens]");
        Assert.False(usage.HasAttribute("data-rgf-usage-tooltip"));
        Assert.Equal("LLM-hívások: 3", usage.GetAttribute("aria-label"));
        Assert.Equal("LLM-hívások", first.Find("[id$=-tokens] dt").TextContent);

        await context.DisposeAsync();
        languageEventLog.Entries.Clear();
        await security.LanguageChangedEvent.InvokeAsync(new("eng"));
        Assert.Empty(languageEventLog.Entries);
        Assert.Equal(2, context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.dispose"].Count);
        Assert.All(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.dispose"], invocation => Assert.IsType<string>(Assert.Single(invocation.Arguments)));
    }
    private static BunitContext Context()
    {
        var context = new BunitContext();
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        RgfClientBlazorUiTestState.LoadUiResources(context);
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
        var duration = cut.Find(".rgf-ai-usage-duration");
        Assert.False(duration.HasAttribute("title"));
        Assert.False(duration.HasAttribute("data-rgf-usage-tooltip"));
        Assert.False(duration.HasAttribute("tabindex"));
        Assert.Equal($"Execution time: {expected.Replace('.', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0])}", duration.GetAttribute("aria-label"));
    }

    [Fact]
    public void EveryReportedTokenFieldAndUnknownIdentityIsDisplayedWithoutFallback()
    {
        using var context = Context();
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, new() { CreditUsage = new() {
            Tokens = new() { InputTokens = 0, OutputTokens = 7, TotalTokens = 99, CachedInputTokens = 3, ReasoningTokens = 2 },
            Models = [new() { Tokens = new() }]
        } }));
        Assert.Equal(new[] { "0", "0", "7", "99", "3", "2" },
            cut.FindAll("[id$=-tokens] dd").Select(e => e.TextContent));
        Assert.Equal(new[] { "Unknown", "Unknown", "Default" }, cut.FindAll("[id$=-models] dd").Select(e => e.TextContent));

        var usage = cut.Instance.Response.CreditUsage!;
        var model = new RgfAiModelCreditUsage { ProviderId = "openai", ModelId = "gpt-6-luna", Tokens = new() {
            InputTokens = 0, OutputTokens = 7, TotalTokens = 99, CachedInputTokens = 3, ReasoningTokens = 2
        } };
        usage.Models = [model];
        cut.Render();
        var panel = cut.Find("[id$=-tokens]");
        Assert.Equal(2, panel.QuerySelectorAll("dl").Length);
        Assert.Equal("Aggregated token statistics", panel.QuerySelector("strong")!.TextContent);
        Assert.Equal("DL", panel.FirstElementChild!.TagName);
        Assert.DoesNotContain("openai / gpt-6-luna", panel.TextContent);
        Assert.DoesNotContain("Request tokens", panel.TextContent);
        Assert.Equal(new[] { "0", "0", "7", "99", "3", "2" }, panel.QuerySelectorAll("dd").Select(e => e.TextContent));

        model.Tokens.ReasoningTokens = null;
        cut.Render();
        Assert.Equal(2, cut.FindAll("[id$=-tokens] dl").Count);
        Assert.Equal("2", cut.FindAll("[id$=-tokens] dd")[5].TextContent);

        model.Tokens.ReasoningTokens = 2;
        usage.Models = [model, new() { ProviderId = "other", ModelId = "other-model", Tokens = model.Tokens }];
        cut.Render();
        Assert.Equal(2, cut.FindAll("[id$=-tokens] dl").Count);
        Assert.DoesNotContain("other-model", cut.Find("[id$=-tokens]").TextContent);
        usage.Tokens!.TotalTokens = null;
        cut.Render();
        Assert.Equal("Unknown", cut.FindAll("[id$=-tokens] dd")[3].TextContent);
        usage.Tokens = null;
        cut.Render();
        Assert.Equal("0", Assert.Single(cut.FindAll("[id$=-tokens] dd")).TextContent);
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
        var button = cut.Find("button[popovertarget$=-cost]");
        var formatted = expected.Replace('.', System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0]);
        Assert.Equal(formatted, button.TextContent.Trim());
        Assert.False(button.HasAttribute("data-rgf-usage-tooltip"));
        Assert.Equal($"Charged credit: {formatted}", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void UsesDistinctServerModelsAndNeverSumsUnknownCostsOrTokens()
    {
        using var context = Context();
        var response = new RgfAiResponse { CreditState = new(), CreditUsage = new() { ExecutionTime = TimeSpan.FromSeconds(72), Models = [
            new() { ProviderId = "p", ModelId = "actual", ChargedCredit = 2 },
            new() { ProviderId = "p", ModelId = "actual", ChargedCredit = 3, Tokens = new() { InputTokens = 0, OutputTokens = 7 } }
        ] } };
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        Assert.Contains("actual", cut.Find(".rgf-ai-usage-item").TextContent);
        Assert.Contains("1m 12s", cut.Markup);
        Assert.Equal(new[] { "bi bi-stopwatch", "bi bi-cpu", "bi bi-bar-chart", "bi bi-coin", "bi bi-piggy-bank" },
            cut.FindAll(".rgf-ai-usage > span > i, .rgf-ai-usage > button > i").Select(icon => icon.ClassName));
        var indicators = cut.FindAll(".rgf-ai-usage > span, .rgf-ai-usage > button");
        Assert.Equal(5, indicators.Count);
        Assert.All(indicators, indicator =>
        {
            Assert.False(indicator.HasAttribute("title"));
            Assert.False(indicator.HasAttribute("data-rgf-usage-tooltip"));
        });
        Assert.Equal("Unknown", cut.Find("[id$=-cost] dd").TextContent);
        Assert.Equal("0", Assert.Single(cut.FindAll("[id$=-tokens] dd")).TextContent);
        response.CreditUsage.Models = [new() { ProviderId = "p", ModelId = "a" }, new() { ProviderId = "q", ModelId = "a" }];
        cut.Render();
        Assert.Contains("2 models", cut.Markup);
        var models = cut.Find("button[popovertarget$=-models]");
        Assert.False(models.HasAttribute("data-rgf-usage-tooltip"));
        Assert.Equal("Models: 2 models", models.GetAttribute("aria-label"));
        Assert.Single(cut.FindAll(".bi-bar-chart"));
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
        Assert.False(button.HasAttribute("data-rgf-usage-tooltip"));
        Assert.Equal($"Credit state at response completion: {label}", button.GetAttribute("aria-label"));
        var icon = button.QuerySelector("i")!;
        Assert.Equal("bi bi-piggy-bank", icon.ClassName);
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Empty(button.QuerySelectorAll(".bi-wallet2, [class*=fill]"));
        Assert.Contains(detail, cut.Find("[role=dialog]").TextContent);
        Assert.Contains("Snapshot at response completion", cut.Markup);
    }

    [Theory]
    [InlineData(true, "Yes")]
    [InlineData(false, "No")]
    public void PrepaidAndIndependentPeriodsPreserveExpiryNegativeBalanceAndUtcSource(bool isExpired, string expiryLabel)
    {
        using var context = Context();
        var response = new RgfAiResponse { CreditState = new() { Mode = AiCreditMode.Prepaid,
            Prepaid = new(-2, new DateOnly(2026, 10, 13), isExpired, DateTime.UtcNow) } };
        var cut = context.Render<RgfAiUsageSummary>(p => p.Add(c => c.Response, response));
        Assert.Contains("-2", cut.Markup);
        Assert.Contains("2026-10-13", cut.Markup);
        Assert.Contains(cut.FindAll("[id$=-credit] dd"), item => item.TextContent == expiryLabel);
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
