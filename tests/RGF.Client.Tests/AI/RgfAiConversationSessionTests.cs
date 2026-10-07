using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;

namespace Recrovit.RecroGridFramework.Client.Tests.AI;

public sealed class RgfAiConversationSessionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Send_ForwardsCurrentTurnAndAppliesResponse(bool display)
    {
        var response = new RgfAiResponse { Success = true, ConversationId = "id", ConversationToken = "token", Message = "answer" };
        var transport = new FakeTransport((_, _) => Task.FromResult(response));
        var state = new RgfAiConversationState { AiModelOverride = "model" };
        var session = new RgfAiConversationSession(transport, state);
        var notifications = new List<(bool Processing, int Count)>();
        session.StateChanged += () => notifications.Add((session.IsProcessing, state.Messages.Count));

        Assert.Same(response, await session.SendAsync("first", display, TestContext.Current.CancellationToken));
        Assert.Same(state, session.Conversation);
        Assert.Equal("first", transport.Requests[0].CurrentUserMessage);
        Assert.Equal("model", transport.Requests[0].AiModelOverride);
        Assert.Null(transport.Requests[0].ConversationId);
        Assert.Equal(display ? 2 : 1, state.Messages.Count);
        Assert.Same(response, state.Messages.Last());
        Assert.Equal(new[] { (true, display ? 1 : 0), (true, display ? 2 : 1), (false, display ? 2 : 1) }, notifications);
        Assert.False(session.IsProcessing);

        await session.SendAsync("second", display, TestContext.Current.CancellationToken);
        Assert.Equal("id", transport.Requests[1].ConversationId);
        Assert.Equal("token", transport.Requests[1].ConversationToken);
        Assert.Equal("second", Assert.Single(transport.Requests[1].Messages).Message);
    }

    [Fact]
    public async Task FirstStateChanged_SeesDisplayedUserMessage()
    {
        var completion = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport((_, token) => completion.Task.WaitAsync(token));
        var session = new RgfAiConversationSession(transport);
        var notifications = new List<(bool Processing, RgfAiMessage[] Messages)>();
        session.StateChanged += () => notifications.Add((session.IsProcessing, session.Conversation.Messages.ToArray()));

        var turn = session.SendAsync("current user message", cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            Assert.Single(transport.Requests);
            Assert.False(completion.Task.IsCompleted);
            Assert.False(turn.IsCompleted);
            var notification = Assert.Single(notifications);
            Assert.True(notification.Processing);
            var message = Assert.Single(notification.Messages);
            Assert.Equal(RgfAiMessageRole.User, message.Role);
            Assert.Equal("current user message", message.Message);
            Assert.Same(message, session.Conversation.Messages.Last());
            Assert.True(session.IsProcessing);
        }
        finally
        {
            completion.TrySetResult(new RgfAiResponse { Success = true, Message = "answer" });
        }
        await turn;
    }

    [Fact]
    public async Task PendingTurn_RejectsConcurrentSendButOtherSessionsCanRun()
    {
        var completion = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = new FakeTransport((_, _) => completion.Task);
        var session = new RgfAiConversationSession(transport);
        var first = session.SendAsync("first", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(session.IsProcessing);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("second", cancellationToken: TestContext.Current.CancellationToken));
        var other = new RgfAiConversationSession(transport);
        var independent = other.SendAsync("independent", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(other.IsProcessing);
        Assert.Equal(2, transport.Requests.Count);
        completion.SetResult(new RgfAiResponse { Success = true });
        await Task.WhenAll(first, independent);
        Assert.False(session.IsProcessing);
        Assert.False(other.IsProcessing);
        Assert.Equal(2, session.Conversation.Messages.Count);
    }

    [Fact]
    public async Task TransportException_PropagatesAndRestoresProcessingWithoutSyntheticResponse()
    {
        var failure = new IOException("transport");
        var session = new RgfAiConversationSession(new FakeTransport((_, _) => Task.FromException<RgfAiResponse>(failure)));
        var states = new List<bool>();
        session.StateChanged += () => states.Add(session.IsProcessing);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => session.SendAsync("turn", cancellationToken: TestContext.Current.CancellationToken)));
        Assert.False(session.IsProcessing);
        Assert.Equal(new[] { true, false }, states);
        Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
    }

    [Fact]
    public async Task Cancellation_ForwardsTokenAndRestoresProcessing()
    {
        using var cancellation = new CancellationTokenSource();
        var session = new RgfAiConversationSession(new FakeTransport(async (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            await Task.Delay(Timeout.Infinite, token);
            return new();
        }));
        var turn = session.SendAsync("turn", cancellationToken: cancellation.Token);
        Assert.True(session.IsProcessing);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => turn);
        Assert.False(session.IsProcessing);
        Assert.Single(session.Conversation.Messages);
    }

    [Fact]
    public async Task FailedAiResponse_IsReturnedAndUsesStateFailureSemantics()
    {
        var state = new RgfAiConversationState();
        state.ApplyResponse(new() { Success = true, ConversationId = "id", ConversationToken = "token" });
        var response = new RgfAiResponse { Success = false, ConversationId = "failed", ConversationToken = "failed" };
        var session = new RgfAiConversationSession(new FakeTransport((_, _) => Task.FromResult(response)), state);
        Assert.Same(response, await session.SendAsync("turn", false, TestContext.Current.CancellationToken));
        Assert.Equal("id", state.ConversationId);
        Assert.Equal("token", state.ConversationToken);
        Assert.Single(state.Messages);
        Assert.False(session.IsProcessing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task InvalidInstruction_IsRejectedBeforeTransportOrStateChanges(string? instruction)
    {
        var transport = new FakeTransport((_, _) => Task.FromResult(new RgfAiResponse()));
        var session = new RgfAiConversationSession(transport);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => session.SendAsync(instruction!, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Empty(transport.Requests);
        Assert.Empty(session.Conversation.Messages);
        Assert.False(session.IsProcessing);
    }

    private sealed class FakeTransport(Func<RgfAiRequest, CancellationToken, Task<RgfAiResponse>> send) : IRgfAiTransport
    {
        public List<RgfAiRequest> Requests { get; } = [];
        public Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return send(request, cancellationToken);
        }
    }
}
