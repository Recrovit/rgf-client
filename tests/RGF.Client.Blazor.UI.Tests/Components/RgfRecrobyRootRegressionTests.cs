using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealRootRetainsNavbarDashboardDialogAndToastAcrossDocking(bool enabled)
    {
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddRgfServices(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Recrovit:RecroGridFramework:API:BaseAddress"] = "https://api.example.test" }).Build());
        context.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        var api = new FakeRgfApiService();
        context.Services.AddSingleton<IRgfApiService>(api);
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
        var navbar = cut.FindComponent<NavbarComponent>().Instance;
        var dashboard = cut.FindComponent<DashboardPageComponent>().Instance;
        var dialog = cut.FindComponent<DialogComponent>().Instance;
        var toast = cut.FindComponent<ToastComponent>().Instance;
        if (enabled)
        {
            cut.Find("[aria-label='Open Recroby']").Click();
            foreach (var mode in new[] { "DockLeft", "DockRight", "DockBottom", "Floating" })
            {
                cut.Find($"[data-dock={mode}]").Click();
                Assert.Same(navbar, cut.FindComponent<NavbarComponent>().Instance);
                Assert.Same(dashboard, cut.FindComponent<DashboardPageComponent>().Instance);
                Assert.Same(dialog, cut.FindComponent<DialogComponent>().Instance);
                Assert.Same(toast, cut.FindComponent<ToastComponent>().Instance);
            }
            var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
            cut.Render(p => p.Add(c => c.ChildContent, "<main>Next page</main>"));
            Assert.Same(session, cut.FindComponent<RgfAiChatComponent>().Instance.Session);
        }
        var manager = context.Services.GetRequiredService<IRgfEventNotificationService>().GetNotificationManager(RgfToastEventArgs.NotificationManagerScope);
        await cut.InvokeAsync(() => manager.RaiseEventAsync(new RgfToastEventArgs("Toast", "Still working", delay: 0), this));
        cut.WaitForAssertion(() => Assert.Contains("Still working", cut.Find(".toast-container").TextContent));
        Assert.DoesNotContain(api.Requests, r => r.Uri == "/api/rgf/ai/recroby");
        if (!enabled)
            Assert.DoesNotContain(context.JSInterop.Invocations, call => call.Identifier == "import" && call.Arguments.Any(a => a?.ToString()?.EndsWith("RgfRecrobyWorkspace.razor.js") == true));
    }
}
