using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI;

namespace Recrovit.RecroGridFramework.Client.Tests.AI;

public sealed class RgfAiConversationStateTests
{
    [Fact]
    public void AddUserMessage_AppendsDisplayedUserMessage()
    {
        var state = new RgfAiConversationState();

        state.AddUserMessage("Hello");

        var message = Assert.Single(state.Messages);
        Assert.Equal(RgfAiMessageRole.User, message.Role);
        Assert.Equal("Hello", message.Message);
    }

    [Fact]
    public void Messages_CannotBeModifiedThroughCollectionInterface()
    {
        var state = new RgfAiConversationState();
        state.AddUserMessage("Hello");

        var collection = Assert.IsAssignableFrom<ICollection<RgfAiMessage>>(state.Messages);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
        Assert.Single(state.Messages);
    }

    [Fact]
    public void CreateRequest_UsesIdentityAndModelWithoutAddingPresentationMessage()
    {
        var state = new RgfAiConversationState { AiModelOverride = "selected-model" };
        state.ApplyResponse(SuccessResponse());
        var history = state.Messages.ToArray();

        var request = state.CreateRequest("Hello");

        Assert.Equal("Hello", request.CurrentUserMessage);
        Assert.Equal("conversation-1", request.ConversationId);
        Assert.Equal("token-1", request.ConversationToken);
        Assert.Equal("selected-model", request.AiModelOverride);
        Assert.Equal(history, state.Messages);
    }

    [Fact]
    public void CreateRequest_OnNewConversationLeavesPresentationHistoryEmpty()
    {
        var state = new RgfAiConversationState();

        var request = state.CreateRequest("Hello");

        Assert.Empty(state.Messages);
        Assert.Null(request.ConversationId);
        Assert.Null(request.ConversationToken);
        Assert.Null(request.AiModelOverride);
        Assert.Equal("Hello", request.CurrentUserMessage);
    }

    [Fact]
    public void ApplyResponse_SuccessUpdatesIdentityAndAddsAssistantResponse()
    {
        var state = new RgfAiConversationState();
        state.ApplyResponse(SuccessResponse());
        var response = SuccessResponse("conversation-2", "token-2");

        state.ApplyResponse(response);

        Assert.Equal("conversation-2", state.ConversationId);
        Assert.Equal("token-2", state.ConversationToken);
        Assert.Equal(2, state.Messages.Count);
        Assert.Same(response, state.Messages[1]);
        Assert.Equal(RgfAiMessageRole.Assistant, state.Messages[1].Role);
    }

    [Fact]
    public void ApplyResponse_FailurePreservesIdentityAndHistory()
    {
        var state = new RgfAiConversationState();
        state.ApplyResponse(SuccessResponse());
        var history = state.Messages.ToArray();

        state.ApplyResponse(new RgfAiResponse
        {
            Success = false,
            ConversationId = "failed-conversation",
            ConversationToken = "failed-token",
            Message = "Failed"
        });

        Assert.Equal("conversation-1", state.ConversationId);
        Assert.Equal("token-1", state.ConversationToken);
        Assert.Equal(history, state.Messages);
    }

    [Fact]
    public void NextTurn_UsesPreviousResponseIdentity()
    {
        var state = new RgfAiConversationState();
        state.AddUserMessage("Hello");
        state.ApplyResponse(SuccessResponse());

        state.AddUserMessage("Continue");
        var request = state.CreateRequest("Continue");

        Assert.Equal("conversation-1", request.ConversationId);
        Assert.Equal("token-1", request.ConversationToken);
        Assert.Equal("Continue", request.CurrentUserMessage);
        Assert.Equal(3, state.Messages.Count);
        Assert.Equal("Continue", state.Messages[2].Message);
    }

    [Fact]
    public void CreateRequest_DoesNotCopyPresentationHistoryOrShareItsMessages()
    {
        var state = new RgfAiConversationState();
        state.AddUserMessage("Earlier question");
        state.ApplyResponse(SuccessResponse());
        state.AddUserMessage("Current question");

        var request = state.CreateRequest("Current question");

        var wireMessage = Assert.Single(request.Messages);
        Assert.Equal(RgfAiMessageRole.User, wireMessage.Role);
        Assert.Equal("Current question", wireMessage.Message);
        Assert.NotSame(state.Messages[2], wireMessage);
        wireMessage.Message = "Modified instruction";
        request.Messages.Clear();
        Assert.Equal(3, state.Messages.Count);
        Assert.Equal("Current question", state.Messages[2].Message);
    }

    [Fact]
    public void Reset_ClearsIdentityAndHistoryWhilePreservingModel()
    {
        var state = new RgfAiConversationState { AiModelOverride = "selected-model" };
        state.AddUserMessage("Hello");
        state.ApplyResponse(SuccessResponse());
        var history = state.Messages;

        state.Reset();

        Assert.Empty(state.Messages);
        Assert.Empty(history);
        Assert.Null(state.ConversationId);
        Assert.Null(state.ConversationToken);
        Assert.Equal("selected-model", state.AiModelOverride);
        var request = state.CreateRequest("New conversation");
        Assert.Null(request.ConversationId);
        Assert.Null(request.ConversationToken);
        Assert.Equal("selected-model", request.AiModelOverride);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void InvalidMessage_IsRejectedWithoutChangingHistory(string? message)
    {
        var state = new RgfAiConversationState();

        Assert.ThrowsAny<ArgumentException>(() => state.AddUserMessage(message!));
        Assert.ThrowsAny<ArgumentException>(() => state.CreateRequest(message!));
        Assert.Empty(state.Messages);
    }

    [Fact]
    public void ApplyResponse_NullIsRejectedWithoutChangingState()
    {
        var state = new RgfAiConversationState();
        state.ApplyResponse(SuccessResponse());

        Assert.Throws<ArgumentNullException>(() => state.ApplyResponse(null!));

        Assert.Single(state.Messages);
        Assert.Equal("conversation-1", state.ConversationId);
        Assert.Equal("token-1", state.ConversationToken);
    }

    private static RgfAiResponse SuccessResponse(string id = "conversation-1", string token = "token-1") => new()
    {
        Success = true,
        ConversationId = id,
        ConversationToken = token,
        Message = "Assistant answer"
    };
}
