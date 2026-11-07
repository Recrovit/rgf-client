using Bunit;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using System.Reflection;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed partial class RgfRecrobyWorkspaceTests
{
    [Fact]
    public async Task Filter_model_selection_requires_expanded_workspace_and_explicit_active_choice()
    {
        using var context = Context();
        var cut = context.Render<RgfRecrobyWorkspace>();
        var session = cut.FindComponent<RgfAiChatComponent>().Instance.Session;
        var selection = typeof(RgfRecrobyWorkspace).GetProperty("NameDescriptionModelSelection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(selection.GetValue(cut.Instance));
        session.Conversation.AiModelOverride = "provider/model";
        Assert.Null(selection.GetValue(cut.Instance));
        await cut.InvokeAsync(() => cut.Instance.LayoutChanged(new() { IsCollapsed = false }));
        Assert.Equal(("provider", "model"), selection.GetValue(cut.Instance));
        Assert.Equal("provider/model", session.Conversation.AiModelOverride);
        session.Conversation.AiModelOverride = "malformed";
        Assert.Equal(("", "malformed"), selection.GetValue(cut.Instance));
        session.Conversation.AiModelOverride = null;
        Assert.Null(selection.GetValue(cut.Instance));
        Assert.Empty(session.Conversation.Messages);
    }
}
