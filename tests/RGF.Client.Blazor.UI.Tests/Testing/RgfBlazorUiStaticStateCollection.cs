using Xunit;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RgfBlazorUiStaticStateCollection
{
    public const string Name = "RGF.Blazor.UI.StaticState";
}
