using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Dashboard;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;
using Recrovit.RecroGridFramework.Client.Events;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

[Collection(RgfBlazorUiStaticStateCollection.Name)]
public sealed class RgfRecrobyRootRegressionTests : IDisposable
{
    public RgfRecrobyRootRegressionTests() => RgfClientBlazorUiTestState.Reset();
    public void Dispose() => RgfClientBlazorUiTestState.Reset();

    [Fact]
    public async Task RootRegistersResourcesBeforeItsChildrenRender()
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddRgfServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://api.example.test" }).Build());
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRgfApiService, FakeRgfApiService>();
        var startLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workspaceInitialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.JSInterop.SetupVoid("Recrovit.RGF.Blazor.UI.AI.Workspace.initialize", _ =>
        {
            workspaceInitialized.TrySetResult();
            return true;
        }).SetVoidResult();

        var cut = context.Render<DeferredRoot>(p => p.Add(c => c.StartLoad, startLoad.Task));

        Assert.Single(cut.FindComponents<RgfRecrobyWorkspace>());
        Assert.Empty(context.JSInterop.Invocations["import"]);
        Assert.Empty(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Workspace.initialize"]);

        startLoad.SetResult();
        await WithTimeout(workspaceInitialized.Task);
    }

    [Fact]
    public async Task FailedImportReachesCurrentWaitersAndAnExplicitRetryCanSucceed()
    {
        using var context = CreateUsageContext();
        using var otherContext = CreateUsageContext();
        var importGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jsRuntime = new GatedAiImport(context.JSInterop.JSRuntime, importGate.Task);
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        var load = RGFClientBlazorUIConfiguration.LoadResourcesAsync(context.Services);
        var first = RenderUsage<ObservedUsage>(context).Instance.AfterRender;
        var second = RenderUsage<ObservedUsage>(context).Instance.AfterRender;
        Assert.Single(context.JSInterop.Invocations["import"], IsAiImport);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Empty(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.sync"]);

        var missing = Assert.Throws<InvalidOperationException>(() => RenderUsage<RgfAiUsageSummary>(otherContext));
        Assert.Contains("Initialize the central RGF UI resources", missing.Message);
        Assert.Empty(otherContext.JSInterop.Invocations["import"]);

        var failure = new JSException("AI import failed");
        importGate.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<JSException>(() => WithTimeout(load)));
        jsRuntime.ImportGate = Task.CompletedTask;
        await RGFClientBlazorUIConfiguration.LoadResourcesAsync(context.Services);
        Assert.Same(failure, await Assert.ThrowsAsync<JSException>(() => WithTimeout(first)));
        Assert.Same(failure, await Assert.ThrowsAsync<JSException>(() => WithTimeout(second)));
        Assert.Empty(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.sync"]);

        await WithTimeout(RenderUsage<ObservedUsage>(context).Instance.AfterRender);
        Assert.Single(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.sync"]);
        var imports = context.JSInterop.Invocations["import"];
        Assert.Equal(2, imports.Count(IsAiImport));
        Assert.All(imports.Where(call => !IsAiImport(call)).GroupBy(call => call.Arguments[0]), group => Assert.Single(group));
    }

    [Fact]
    public async Task DisposedWaiterSkipsInteropAndThemeFailurePreservesAiReadiness()
    {
        using var context = CreateUsageContext();
        var importGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jsRuntime = new GatedAiImport(context.JSInterop.JSRuntime, importGate.Task);
        context.Services.AddSingleton<IJSRuntime>(jsRuntime);
        var theme = context.JSInterop.SetupVoid("eval", call => call.Arguments[0] is string script
            && script.Contains("setAttribute('data-bs-theme'", StringComparison.Ordinal));
        var load = RGFClientBlazorUIConfiguration.LoadResourcesAsync(context.Services);
        var disposed = RenderUsage<ObservedUsage>(context);
        var disposedRender = disposed.Instance.AfterRender;
        var activeRender = RenderUsage<ObservedUsage>(context).Instance.AfterRender;
        Assert.False(disposedRender.IsCompleted);
        Assert.False(activeRender.IsCompleted);
        await disposed.InvokeAsync(() => disposed.Instance.DisposeAsync().AsTask());

        importGate.SetResult();
        await WithTimeout(disposedRender);
        await WithTimeout(activeRender);
        Assert.Single(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.sync"]);
        Assert.False(load.IsCompleted);

        var failure = new JSException("Theme setup failed");
        theme.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<JSException>(() => WithTimeout(load)));
        await WithTimeout(RenderUsage<ObservedUsage>(context).Instance.AfterRender);
        Assert.Equal(2, context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Usage.sync"].Count);
        Assert.Single(context.JSInterop.Invocations["import"], IsAiImport);
    }

    private static BunitContext CreateUsageContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        context.Services.AddSingleton<IRgfApiService, FakeRgfApiService>();
        return context;
    }

    private static IRenderedComponent<T> RenderUsage<T>(BunitContext context) where T : RgfAiUsageSummary
        => context.Render<T>(p => p.Add(c => c.Response, new() { CreditUsage = new() }));

    private static bool IsAiImport(JSRuntimeInvocation call)
        => IsAiImport(call.Identifier, call.Arguments[0]);

    private static bool IsAiImport(string identifier, object? argument)
        => identifier == "import" && argument is string url
            && url.Contains("/recrovit-rgf-blazor-ui-ai.", StringComparison.Ordinal);

    private static Task WithTimeout(Task task)
        => task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

    private sealed class DeferredRoot : RgfRootComponent
    {
        [Parameter]
        public Task StartLoad { get; set; } = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await StartLoad;
            await base.OnAfterRenderAsync(firstRender);
        }
    }

    private sealed class ObservedUsage : RgfAiUsageSummary
    {
        public Task AfterRender { get; private set; } = default!;

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            // Capture each component's lifecycle task so both failures can be observed.
            AfterRender = base.OnAfterRenderAsync(firstRender);
            return Task.CompletedTask;
        }
    }

    private sealed class GatedAiImport(IJSRuntime inner, Task importGate) : IJSRuntime
    {
        public Task ImportGate { private get; set; } = importGate;

        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args)
            => InvokeAsync<T>(identifier, CancellationToken.None, args);

        public async ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            var result = await inner.InvokeAsync<T>(identifier, cancellationToken, args);
            if (IsAiImport(identifier, args?.FirstOrDefault()))
            {
                await ImportGate;
            }
            return result;
        }
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    public async Task RealRootRetainsNavbarDashboardDialogAndToastAcrossDocking(bool enabled, bool backendEnabled, bool capabilitySuccess)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddRgfServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://api.example.test" }).Build());
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        var api = new FakeRgfApiService { RecrobyEnabled = backendEnabled, CapabilitySuccess = capabilitySuccess };
        context.Services.AddSingleton<IRgfApiService>(api);
        var uiStyle = context.JSInterop.Setup<bool>("Recrovit.LPUtils.EnsureStyleSheetLoaded",
            invocation => Equals(invocation.Arguments[0], "rgf-check-stylesheet-client-blazor-ui"));
        var workspaceInitialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.JSInterop.SetupVoid("Recrovit.RGF.Blazor.UI.AI.Workspace.initialize", _ =>
        {
            workspaceInitialized.TrySetResult();
            return true;
        }).SetVoidResult();
        RenderFragment content = builder =>
        {
            builder.OpenComponent<NavbarComponent>(0);
            builder.AddAttribute(1, "MenuParameters", new RgfMenuParameters { MenuItems = [] });
            builder.AddAttribute(2, "BrandText", "Host navbar");
            builder.CloseComponent();
            builder.OpenComponent<DashboardPageComponent>(3);
            builder.CloseComponent();
            builder.OpenComponent<DialogComponent>(4);
            builder.AddAttribute(5, "DialogParameters", new RgfDialogParameters { Title = "Host dialog" });
            builder.CloseComponent();
        };
        var cut = context.Render<RgfRootComponent>(p => p.Add(c => c.EnableRecroby, enabled).Add(c => c.ChildContent, content));
        var effectiveEnabled = enabled && (!capabilitySuccess || backendEnabled);
        Assert.Equal(effectiveEnabled ? 1 : 0, cut.FindComponents<RgfRecrobyWorkspace>().Count);
        Assert.Empty(context.JSInterop.Invocations["Recrovit.RGF.Blazor.UI.AI.Workspace.initialize"]);
        Assert.Single(uiStyle.Invocations);
        uiStyle.SetResult(true);
        if (effectiveEnabled)
            await workspaceInitialized.Task.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        var navbar = cut.FindComponent<NavbarComponent>().Instance;
        var dashboard = cut.FindComponent<DashboardPageComponent>().Instance;
        var dialog = cut.FindComponent<DialogComponent>().Instance;
        var toast = cut.FindComponent<ToastComponent>().Instance;
        var anchorStyle = cut.Find(".rgf-toast-container").GetAttribute("style");
        Assert.Matches(@"^--rgf-toast-anchor: --rgf-toast-[0-9a-f]{32};$", anchorStyle!);
        Assert.DoesNotContain("end-0", cut.Find(".rgf-toast-container").ClassList);
        if (effectiveEnabled)
        {
            Assert.Equal(anchorStyle, cut.Find(".rgf-recroby-workspace").GetAttribute("style"));
            Assert.Equal(cut.Find(".rgf-toast-container"), cut.Find(".rgf-recroby-workspace").NextElementSibling);
            cut.Find("[aria-label='Open Recroby']").Click();
            foreach (var mode in new[] { "DockLeft", "DockRight", "DockBottom", "Floating" })
            {
                cut.Find($"[data-dock={mode}]").Click();
                Assert.Same(navbar, cut.FindComponent<NavbarComponent>().Instance);
                Assert.Same(dashboard, cut.FindComponent<DashboardPageComponent>().Instance);
                Assert.Same(dialog, cut.FindComponent<DialogComponent>().Instance);
                Assert.Same(toast, cut.FindComponent<ToastComponent>().Instance);
                Assert.Equal(anchorStyle, cut.Find(".rgf-toast-container").GetAttribute("style"));
            }
            var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
            cut.Render(p => p.Add(c => c.ChildContent, "<main>Next page</main>"));
            Assert.Same(session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
        }
        Assert.Equal(anchorStyle, cut.Find(".rgf-toast-container").GetAttribute("style"));
        // A separately rendered Root shares services, but never its CSS anchor identity.
        var second = context.Render<RgfRootComponent>(p => p.Add(c => c.EnableRecroby, enabled));
        var secondStyle = second.Find(".rgf-toast-container").GetAttribute("style");
        Assert.NotEqual(anchorStyle, secondStyle);
        if (effectiveEnabled) Assert.Equal(secondStyle, second.Find(".rgf-recroby-workspace").GetAttribute("style"));
        second.Dispose();
        var manager = context.Services.GetRequiredService<IRgfEventNotificationService>().GetNotificationManager(RgfToastEventArgs.NotificationManagerScope);
        await cut.InvokeAsync(() => manager.RaiseEventAsync(new RgfToastEventArgs("Toast", "Still working", delay: 0), this));
        cut.WaitForAssertion(() => Assert.Contains("Still working", cut.Find(".toast-container").TextContent));
        cut.Find(".toast .btn-close").Click();
        Assert.Empty(cut.FindAll(".toast"));
        var progress = new RgfProgressArgs(25m, "Processing") { IsBackgroundTaskCompleted = false, Footer = "Progress footer" };
        var progressToast = new RgfToastEventArgs("Progress", "Task", delay: 0, progressArgs: progress);
        await cut.InvokeAsync(() => manager.RaiseEventAsync(progressToast, this));
        cut.WaitForAssertion(() => Assert.Contains("25%", cut.Find(".toast-header").TextContent));
        Assert.Contains("Processing", cut.Find(".progress-message").TextContent);
        Assert.Contains("Progress footer", cut.Find(".toast-footer").TextContent);
        Assert.NotEmpty(cut.FindAll(".toast-body .spinner-border"));
        progress.Percentage = 75;
        cut.WaitForAssertion(() => Assert.Contains("75%", cut.Find(".toast-header").TextContent), TimeSpan.FromSeconds(3));
        progressToast.Delay = -1;
        await cut.InvokeAsync(() => manager.RaiseEventAsync(progressToast, this));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".toast")));
        await cut.InvokeAsync(() => manager.RaiseEventAsync(new RgfToastEventArgs("Timed", "Expires", delay: 1000), this));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".toast")));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".toast")), TimeSpan.FromSeconds(3));
        Assert.DoesNotContain(api.Requests, r => r.Uri == "/api/rgf/ai/recroby");
        if (!effectiveEnabled)
        {
            var gridService = context.Services.GetRequiredService<Recrovit.RecroGridFramework.Client.AI.RgfRecrobyWorkspaceService>();
            Assert.False(gridService.CanOpenGrid);
            Assert.False(await gridService.OpenOrActivateGridAsync(new()));
            await using var owner = context.Services.GetRequiredService<Recrovit.RecroGridFramework.Client.AI.IRgfRecrobyWorkspaceService>().CreateOwner();
            Assert.False(await owner.OpenOrActivateAsync("custom", "a", "a", () => throw new Exception("Disabled Workspace must not call the factory")));
            Assert.DoesNotContain(context.JSInterop.Invocations,
                call => call.Identifier.StartsWith("Recrovit.RGF.Blazor.UI.AI.", StringComparison.Ordinal));
        }
    }
}
