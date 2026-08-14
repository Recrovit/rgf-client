using Bunit;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class SpinnerComponentTests
{
    [Fact]
    public void UsesLoadingIndicatorParametersForTextAndStatus()
    {
        using var testContext = new BunitContext();

        var cut = testContext.Render<SpinnerComponent>(parameters => parameters
            .Add(component => component.LoadingIndicatorParameters, new RgfLoadingIndicatorParameters
            {
                Text = "Please wait",
                Status = "Loading records"
            }));

        Assert.Contains("Loading records", cut.Markup);
        Assert.DoesNotContain("Loading...", cut.Markup);
    }

    [Fact]
    public void UsesLoadingIndicatorParametersForStyle()
    {
        using var testContext = new BunitContext();

        var cut = testContext.Render<SpinnerComponent>(parameters => parameters
            .Add(component => component.LoadingIndicatorParameters, new RgfLoadingIndicatorParameters
            {
                Style = "width: 5rem; height: 5rem;"
            }));

        Assert.Contains("style=\"width: 5rem; height: 5rem;\"", cut.Markup);
    }

    [Fact]
    public void FallsBackToStyleParameterWhenLoadingIndicatorStyleIsMissing()
    {
        using var testContext = new BunitContext();

        var cut = testContext.Render<SpinnerComponent>(parameters => parameters
            .Add(component => component.Style, "width: 2rem; height: 2rem;")
            .Add(component => component.LoadingIndicatorParameters, new RgfLoadingIndicatorParameters()));

        Assert.Contains("style=\"width: 2rem; height: 2rem;\"", cut.Markup);
    }
}
