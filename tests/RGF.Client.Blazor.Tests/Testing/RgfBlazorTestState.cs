using Recrovit.RecroGridFramework.Client.Blazor;

namespace Recrovit.RecroGridFramework.Client.Blazor.Tests.Testing;

internal static class RgfBlazorTestState
{
    public static void Reset()
    {
        foreach (var componentType in Enum.GetValues<RgfBlazorConfiguration.ComponentType>())
        {
            RgfBlazorConfiguration.UnregisterComponent(componentType);
        }

        RgfBlazorConfiguration.ClearEntityComponentTypes();
    }
}
