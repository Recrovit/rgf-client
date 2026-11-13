using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class DialogComponentTests : IDisposable
{
    private readonly BunitContext _context = new();

    public DialogComponentTests()
    {
        RgfClientBlazorUiTestState.Reset();
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _context.Services.AddLogging();
        _context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        RgfBlazorConfiguration.RegisterComponent<DialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void FullscreenEligibilityUsesDefaultAndRespectsOptOutAndInline(bool optOut, bool inline, bool eligible)
    {
        var parameters = new RgfDialogParameters { IsInline = inline, Width = "700px", UniqueName = "filter", Resizable = true };
        Assert.True(parameters.FullscreenOnMobile);
        if (optOut)
        {
            parameters.FullscreenOnMobile = false;
        }

        var dialog = Render(parameters);
        Assert.Equal(eligible, dialog.Find("[role=dialog]").HasAttribute("data-rgf-fullscreen-on-mobile"));
        Assert.Equal(!inline, dialog.FindAll(".modal-dialog").Count == 1);
        Assert.Equal(!inline, dialog.FindAll(".modal-backdrop").Count == 1);
        Assert.Contains("width:700px", dialog.Find(".modal-content").GetAttribute("style")!);

        var initialize = Assert.Single(_context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.Dialog.initialize"]);
        Assert.Equal(true, initialize.Arguments[1]);
        Assert.Equal("filter", initialize.Arguments[2]);
        Assert.Equal(inline, initialize.Arguments[4]);
        Assert.Equal(eligible, initialize.Arguments[5]);
    }

    [Fact]
    public async Task HeaderlessFormParametersProvideAWorkingMobileCloseButton()
    {
        var parameters = new RgfFormParameters().DialogParameters;
        parameters.NoHeader = true;
        parameters.MobileTitle = "Record";
        var closed = false;
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, _ => closed = true, true);
        var dialog = Render(parameters);

        Assert.Contains("Record", dialog.Find(".modal-header.rgf-mobile-only").TextContent);
        await dialog.Find(".btn-close.rgf-mobile-only").ClickAsync(new MouseEventArgs());
        Assert.True(closed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExplicitlyHiddenCloseButtonAlsoBlocksEscape(bool fullscreenOnMobile)
    {
        var parameters = new RgfDialogParameters { ShowCloseButton = false, FullscreenOnMobile = fullscreenOnMobile };
        var closed = false;
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, _ => closed = true, true);
        var dialog = Render(parameters);

        Assert.Empty(dialog.FindAll(".btn-close"));
        await dialog.InvokeAsync(() => dialog.Instance.OnKeyDownJsCallback(new KeyboardEventArgs { Code = "Escape" }));
        Assert.False(closed);
    }

    [Fact]
    public async Task StandardDialogRetainsHeaderCloseAndEscape()
    {
        var parameters = new RgfDialogParameters { Title = "Message", ShowCloseButton = true };
        var closes = 0;
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, _ => { closes++; }, true);
        var dialog = Render(parameters);

        Assert.Equal("Message", dialog.Find(".modal-title").TextContent);
        await dialog.Find(".btn-close:not(.rgf-mobile-only)").ClickAsync(new MouseEventArgs());
        await dialog.InvokeAsync(() => dialog.Instance.OnKeyDownJsCallback(new KeyboardEventArgs { Code = "Escape" }));
        Assert.Equal(2, closes);
    }

    [Fact]
    public async Task InlineDialogRetainsEscapeWithoutAHeaderCloseButton()
    {
        var parameters = new RgfDialogParameters { IsInline = true, NoHeader = true, ShowCloseButton = false };
        var closed = false;
        parameters.EventDispatcher.Subscribe(RgfDialogEventKind.Destroy, _ => closed = true, true);
        var dialog = Render(parameters);

        Assert.Empty(dialog.FindAll(".modal-header"));
        await dialog.InvokeAsync(() => dialog.Instance.OnKeyDownJsCallback(new KeyboardEventArgs { Code = "Escape" }));
        Assert.True(closed);
    }

    [Fact]
    public async Task StackedChoiceAndAsyncConfirmationRequireAnAnswerAndKeepTheUnderlyingDialog()
    {
        var host = _context.Render<RgfDynamicDialog>();
        var chosen = 0;
        await host.InvokeAsync(() => host.Instance.Choice("Choice", "First question",
            [new ButtonParameters("Continue", _ => { chosen++; }, isPrimary: true)]));
        var lower = host.FindComponent<DialogComponent>().Instance;
        Task<ApprovalType> result = null!;
        await host.InvokeAsync(() =>
        {
            result = host.Instance.PromptActionConfirmationAsync("Confirm", "Second question", ApprovalType.Yes | ApprovalType.Cancel);
        });

        Assert.Equal(2, host.FindAll("[data-rgf-fullscreen-on-mobile=true]").Count);
        Assert.Empty(host.FindAll(".btn-close"));
        var upper = host.FindComponents<DialogComponent>()[1];
        await upper.InvokeAsync(() => upper.Instance.OnKeyDownJsCallback(new KeyboardEventArgs { Code = "Escape" }));
        Assert.False(result.IsCompleted);
        Assert.Equal(2, host.Instance.DialogCount);

        await upper.Find(".modal-footer .btn-primary").ClickAsync(new MouseEventArgs());
        Assert.True(result.IsCompletedSuccessfully);
        Assert.Equal(ApprovalType.Cancel, await result);
        Assert.Same(lower, host.FindComponent<DialogComponent>().Instance);
        Assert.Equal(0, chosen);

        await host.Find(".modal-footer .btn-primary").ClickAsync(new MouseEventArgs());
        Assert.Equal(1, chosen);
        Assert.Equal(0, host.Instance.DialogCount);
    }

    [Fact]
    public void DynamicChildHasItsOwnDialogOutsideTheParentScrollableBody()
    {
        var child = new RgfDialogParameters { Title = "Selector", ShowCloseButton = true };
        var parent = Render(new RgfDialogParameters { NoHeader = true, DynamicChild = RgfDynamicDialog.Create(child) });

        Assert.Equal(2, parent.FindAll("[data-rgf-fullscreen-on-mobile=true]").Count);
        Assert.Empty(parent.FindAll(".modal-body [role=dialog]"));
        Assert.Equal("Selector", parent.Find(".modal .modal .modal-title").TextContent);
    }

    private IRenderedComponent<DialogComponent> Render(RgfDialogParameters parameters) =>
        _context.Render<DialogComponent>(p => p.Add(c => c.DialogParameters, parameters));

    public void Dispose()
    {
        _context.Dispose();
        RgfClientBlazorUiTestState.Reset();
    }
}
