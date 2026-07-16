using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Events;
using Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components;
using System.Security.Claims;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal static class DashboardPageTestServices
{
    public static BunitContext CreateContext(FakeRgfApiService apiService)
    {
        var testContext = new BunitContext();
        testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        testContext.Services.AddLogging();
        testContext.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        testContext.Services.AddSingleton<IRecroSecService, FakeRecroSecService>();
        testContext.Services.AddSingleton<IRgfApiService>(apiService);
        RgfBlazorConfiguration.RegisterComponent<DialogComponent>(RgfBlazorConfiguration.ComponentType.Dialog);
        return testContext;
    }
}

internal sealed class FakeRecroSecService : IRecroSecService
{
    public EventDispatcher<EventArgs> AuthenticationStateChanged { get; } = new();
    public EventDispatcher<DataEventArgs<RgfUserState>> UserStateChangedEvent { get; } = new();
    public string? UserName => null;
    public bool IsAuthenticated => false;
    public bool IsAdmin => UserState.IsAdmin;
    public List<string> RoleClaim { get; } = [];
    public ClaimsPrincipal CurrentUser { get; } = new(new ClaimsIdentity());
    public RgfUserState UserState { get; } = new();
    public IReadOnlyDictionary<string, string> Roles => UserState.Roles ?? new Dictionary<string, string>();
    public string UserLanguage => "eng";
    public EventDispatcher<DataEventArgs<string>> LanguageChangedEvent { get; } = new();
    public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(null);
    public Task<string?> SetUserLanguageAsync(string? language) => Task.FromResult<string?>(null);
    public Task<bool> UpdateUserStateSettingsAsync(IDictionary<string, string?> settings) => Task.FromResult(false);
    public Task<RgfPermissions> GetEntityPermissionsAsync(string entityName, string? objectKey = null, int expiration = 60) => throw new NotSupportedException();
    public Task<RgfPermissions> GetPermissionsAsync(string objectName, string? objectKey = null, int expiration = 60) => throw new NotSupportedException();
    public Task<List<RecroSecResult>> GetPermissionsAsync(IEnumerable<RecroSecQuery> query, int expiration = 60) => throw new NotSupportedException();
}
