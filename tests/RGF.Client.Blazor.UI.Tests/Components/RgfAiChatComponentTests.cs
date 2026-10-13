using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Client.AI;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.AI;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Components.Base;
using Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Components;

public sealed class RgfAiChatComponentTests
{
    [Theory]
    [InlineData("AiCredit.UserDisabled", false, true)]
    [InlineData("AiProviderNotConfigured", false, true)]
    [InlineData("AiCredit.Unknown", false, false)]
    [InlineData("AiCredit.Unknown", true, true)]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    public async Task FailedResponseUsageFollowsAlertVisibilityWithoutChangingSafeText(string? code, bool showErrors, bool visible)
    {
        using var context = CreateContext();
        if (code == "AiProviderNotConfigured") code = RgfAiErrorCodes.AiProviderNotConfigured;
        var response = new RgfAiResponse { ErrorCode = code, Message = "<b>Hidden server detail</b>",
            CreditUsage = new() { ChargedCredit = 0 }, CreditState = new() };
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => Task.FromResult(response) });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session).Add(c => c.ShowSendErrors, showErrors));
        cut.Find("textarea").Change("Question");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Equal(visible ? 1 : 0, cut.FindAll(".rgf-ai-usage").Count);
        Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
        if (visible)
        {
            var alert = cut.Find("[role=alert]");
            Assert.Empty(alert.Children);
            Assert.DoesNotContain("Unknown", alert.TextContent);
            Assert.Same(response, cut.FindComponent<RgfAiUsageSummary>().Instance.Response);
            Assert.Equal(alert, cut.Find(".rgf-ai-usage").PreviousElementSibling);
            if (code == "AiCredit.UserDisabled") Assert.Equal(response.Message, alert.TextContent);
            else Assert.DoesNotContain("Hidden server detail", cut.Markup);
        }
        else Assert.DoesNotContain("Hidden server detail", cut.Markup);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewRequestClearsFailedUsageBeforeCompletionAndDoesNotInventExceptionUsage(bool external)
    {
        using var context = CreateContext();
        var first = true;
        var pending = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => first
            ? Task.FromResult(new RgfAiResponse { ErrorCode = "AiCredit.UserDisabled", CreditUsage = new() }) : pending.Task });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session).Add(c => c.ShowSendErrors, true));
        cut.Find("textarea").Change("First");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll(".rgf-ai-usage"));
        first = false;
        Task turn;
        if (external) turn = session.SendAsync("External", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        else
        {
            cut.Find("textarea").Change("Next");
            turn = cut.Find(".input-group ~ button").ClickAsync(new MouseEventArgs());
        }
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".rgf-ai-usage, [role=alert]")));
        pending.SetException(new HttpRequestException("Hidden transport detail"));
        if (external) await Assert.ThrowsAsync<HttpRequestException>(() => turn);
        else await turn;
        Assert.Empty(cut.FindAll(".rgf-ai-usage, .message.text-bg-secondary"));
    }

    [Fact]
    public async Task SessionReplacementClearsFailureAndIgnoresOldInFlightFailure()
    {
        using var context = CreateContext();
        var oldResponse = new RgfAiResponse { ErrorCode = "AiCredit.UserDisabled", Message = "Old denied", CreditUsage = new() };
        var first = true;
        var pending = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => first ? Task.FromResult(oldResponse) : pending.Task });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        cut.Find("textarea").Change("First");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll(".rgf-ai-usage"));
        var other = new RgfAiConversationSession(new TestTransport());
        cut.Render(p => p.Add(c => c.Session, other));
        Assert.Empty(cut.FindAll(".rgf-ai-usage, [role=alert]"));
        cut.Render(p => p.Add(c => c.Session, session));
        first = false;
        cut.Find("textarea").Change("Pending");
        var turn = cut.Find("button").ClickAsync(new MouseEventArgs());
        cut.Render(p => p.Add(c => c.Session, other));
        pending.SetResult(oldResponse);
        await turn;
        Assert.Empty(cut.FindAll(".rgf-ai-usage, [role=alert]"));
        Assert.Empty(other.Conversation.Messages);
    }
    [Fact]
    public async Task UsageUsesOriginalResponsesPreservesBubblesAndIsolatesResetAndSessionReplacement()
    {
        using var context = CreateContext();
        var response = new RgfAiResponse { Success = true, Message = "Original answer",
            CreditUsage = new() { ChargedCredit = 0, Models = [new() { ProviderId = "p", ModelId = "server-model" }] } };
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => Task.FromResult(response) });
        session.Conversation.AddUserMessage("Earlier question");
        session.Conversation.ApplyResponse(response);
        session.Conversation.ApplyResponse(Response("No usage"));
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        var usage = cut.FindAll(".rgf-ai-usage");
        Assert.Single(usage);
        Assert.Same(response, cut.FindComponents<RgfAiUsageSummary>()[0].Instance.Response);
        Assert.Equal("Original answer", usage[0].PreviousElementSibling!.TextContent.Trim());
        Assert.Equal("message text-bg-secondary align-self-start", usage[0].PreviousElementSibling!.ClassName);
        Assert.Single(cut.FindAll(".message.text-bg-primary.align-self-end"));
        var originalId = cut.Find("[popover]").Id!;
        await session.SendAsync("New question", cancellationToken: Xunit.TestContext.Current.CancellationToken);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".rgf-ai-usage").Count));
        Assert.Equal(originalId, cut.Find("[popover]").Id);
        Assert.Equal(4, cut.FindAll("[popover]").Select(e => e.Id).Distinct().Count());

        var other = new RgfAiConversationSession(new TestTransport());
        other.Conversation.ApplyResponse(new() { Success = true, Message = "Other answer", CreditState = new() });
        var second = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, other));
        Assert.DoesNotContain("server-model", second.Markup);
        session.Conversation.Reset();
        cut.Render();
        Assert.Empty(cut.FindAll(".message, .rgf-ai-usage, [popover]"));
        Assert.Single(second.FindAll(".rgf-ai-usage"));
        cut.Render(p => p.Add(c => c.Session, other));
        Assert.Equal("Other answer", cut.Find(".message").TextContent.Trim());
        Assert.DoesNotContain(originalId, cut.Markup);
        Assert.NotEqual(second.Find("[popover]").Id, cut.Find("[popover]").Id);
    }
    [Fact]
    public void RendersUserAndAssistantWithExistingPresentationClasses()
    {
        using var context = CreateContext();
        var session = new RgfAiConversationSession(new TestTransport());
        session.Conversation.AddUserMessage("Question");
        session.Conversation.ApplyResponse(Response("Answer"));
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));

        Assert.Equal("Question", cut.Find(".message.text-bg-primary.align-self-end").TextContent.Trim());
        Assert.Equal("Answer", cut.Find(".message.text-bg-secondary.align-self-start").TextContent.Trim());
        Assert.True(cut.FindComponent<RgfInputText>().Instance.Multiline);
        Assert.Empty(cut.FindComponents<SpinnerComponent>());
        var send = cut.Find("button[aria-label=Send]");
        Assert.Equal("Send", send.GetAttribute("title"));
        Assert.Empty(send.TextContent.Trim());
        Assert.Equal("true", send.QuerySelector(".bi-send")!.GetAttribute("aria-hidden"));
    }

    [Fact]
    public async Task SendTrimsInstructionClearsPromptAndAddsManualTurnToHistory()
    {
        using var context = CreateContext();
        var transport = new TestTransport();
        var session = new RgfAiConversationSession(transport);
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        cut.Find("textarea").Change("  Hello\nworld  ");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Equal("Hello\nworld", Assert.Single(transport.Requests).CurrentUserMessage);
        Assert.Equal(string.Empty, cut.FindComponent<RgfInputText>().Instance.Value);
        Assert.Equal("Hello\nworld", session.Conversation.Messages[0].Message);
        Assert.Equal(RgfAiMessageRole.User, session.Conversation.Messages[0].Role);
        Assert.Equal(2, session.Conversation.Messages.Count);
    }

    [Fact]
    public async Task WhitespacePromptDoesNotSend()
    {
        using var context = CreateContext();
        var transport = new TestTransport();
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, new RgfAiConversationSession(transport)));
        cut.Find("textarea").Change(" \n ");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task ProcessingShowsSpinnerAndDisablesSendUntilCompletion()
    {
        using var context = CreateContext();
        var pending = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => pending.Task });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session).Add(c => c.AllowCancellation, true));
        cut.Find("textarea").Change("Hello");

        var click = cut.Find("button").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<SpinnerComponent>());
            Assert.True(cut.Find("button").HasAttribute("disabled"));
            Assert.Equal(string.Empty, cut.FindComponent<RgfInputText>().Instance.Value);
            var cancel = cut.Find("button[aria-label='Cancel request']");
            Assert.Equal("Cancel request", cancel.GetAttribute("title"));
            Assert.Empty(cancel.TextContent.Trim());
            Assert.Equal("true", cancel.QuerySelector(".bi-stop-circle")!.GetAttribute("aria-hidden"));
        });
        pending.SetResult(Response("Done"));
        await click;

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindComponents<SpinnerComponent>());
            Assert.False(cut.Find("button").HasAttribute("disabled"));
            Assert.Empty(cut.FindAll("button[aria-label='Cancel request']"));
        });
    }

    [Fact]
    public async Task ExternalBackgroundTurnRefreshesPresentationAndProcessing()
    {
        using var context = CreateContext();
        var pending = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => pending.Task });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));

        var turn = Task.Run(() => session.SendAsync("External"));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("External", cut.Markup);
            Assert.Single(cut.FindComponents<SpinnerComponent>());
        });
        pending.SetResult(Response("Background answer"));
        await turn;
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Background answer", cut.Markup);
            Assert.Empty(cut.FindComponents<SpinnerComponent>());
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailureRestoresProcessingWithoutArtificialAssistantMessage(bool useCallback)
    {
        using var context = CreateContext();
        var error = new InvalidOperationException("Transport failed");
        var session = new RgfAiConversationSession(new TestTransport
        {
            Send = _ => Task.FromException<RgfAiResponse>(error)
        });
        var failures = new List<Exception>();
        var cut = context.Render<RgfAiChatComponent>(p =>
        {
            p.Add(c => c.Session, session);
            if (useCallback) p.Add(c => c.SendFailed, (Exception exception) => failures.Add(exception));
        });
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
        Assert.False(session.IsProcessing);
        Assert.Empty(cut.FindComponents<SpinnerComponent>());
        Assert.False(cut.Find("button").HasAttribute("disabled"));
        if (useCallback) Assert.Same(error, Assert.Single(failures));
        else Assert.Empty(failures);
    }

    [Fact]
    public async Task ReplacingSessionDetachesOldSessionAndSubscribesOnceToNewSession()
    {
        using var context = CreateContext();
        var oldSession = new RgfAiConversationSession(new TestTransport());
        var newSession = new RgfAiConversationSession(new TestTransport());
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, oldSession));
        cut.Render(p => p.Add(c => c.Session, newSession));
        cut.Render(p => p.Add(c => c.Session, newSession));
        var renderCount = cut.RenderCount;

        await cut.InvokeAsync(() => oldSession.SendAsync("Old session"));
        Assert.Equal(renderCount, cut.RenderCount);
        Assert.DoesNotContain("Old session", cut.Markup);

        await cut.InvokeAsync(() => newSession.SendAsync("New session"));
        Assert.Contains("New session", cut.Markup);
        // A completed turn emits start, response and completion, each with one subscription.
        Assert.Equal(renderCount + 3, cut.RenderCount);
    }

    [Fact]
    public async Task DisposalDetachesSessionAndPreventsFurtherRenders()
    {
        using var context = CreateContext();
        var session = new RgfAiConversationSession(new TestTransport());
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        await cut.InvokeAsync(() => cut.Instance.Dispose());
        var renderCount = cut.RenderCount;

        await cut.InvokeAsync(() => session.SendAsync("After disposal"));
        Assert.Equal(renderCount, cut.RenderCount);
        cut.Dispose();
        await Task.Run(() => session.SendAsync("After removal"));
        Assert.False(session.IsProcessing);
    }

    [Theory]
    [InlineData("AiCredit.UserDisabled", "AI Credit access is disabled for this user.")]
    [InlineData("AiCredit.UserDisabled", "AI Credit access is disabled for Kovács János.")]
    [InlineData("AiCredit.InsufficientCredit", "There is not enough AI Credit to start another model request.")]
    [InlineData("AiCredit.BalanceExpired", "The AI Credit balance has expired.")]
    [InlineData("AiCredit.InvalidConfiguration", "Hidden server provider/model detail")]
    [InlineData("AiCredit.InvalidConfiguration", null)]
    [InlineData("AiCredit.InvalidConfiguration", "")]
    [InlineData("AiCredit.InvalidConfiguration", " \n ")]
    [InlineData("AiCredit.UserNotFound", "An authenticated RGF user is required for AI Credit access.")]
    [InlineData("AiCredit.InfrastructureFailure", "AI Credit access could not be verified. Please try again later.")]
    public async Task KnownCreditRejectionsShowSafeMessageRegardlessOfShowSendErrors(string code, string? message)
    {
        foreach (var showSendErrors in new[] { false, true })
        {
            using var context = CreateContext();
            var session = new RgfAiConversationSession(new TestTransport
            {
                Send = _ => Task.FromResult(new RgfAiResponse { ErrorCode = code, Message = message! })
            });
            var failures = new List<Exception>();
            var cut = context.Render<RgfAiChatComponent>(p => p
                .Add(c => c.Session, session)
                .Add(c => c.ShowSendErrors, showSendErrors)
                .Add(c => c.SendFailed, (Exception exception) => failures.Add(exception)));
            cut.Find("textarea").Change("Hello");

            await cut.Find("button").ClickAsync(new MouseEventArgs());

            var alert = Assert.Single(cut.FindAll("[role=alert]"));
            var configuration = code == "AiCredit.InvalidConfiguration";
            Assert.Equal(configuration ? "configuration" : "credit", alert.GetAttribute("data-error-kind"));
            Assert.Contains("alert-warning", alert.ClassList);
            Assert.Equal(configuration
                ? "Recroby could not complete the request due to a configuration error. Please contact your system administrator."
                : message, alert.TextContent);
            Assert.DoesNotContain("Hidden server", cut.Markup);
            Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
            Assert.Empty(cut.FindAll(".message.text-bg-secondary"));
            Assert.Empty(failures);
            Assert.False(session.IsProcessing);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProviderConfigurationErrorRetainsFixedMessageRegardlessOfShowSendErrors(bool showSendErrors)
    {
        using var context = CreateContext();
        var session = new RgfAiConversationSession(new TestTransport
        {
            Send = _ => Task.FromResult(new RgfAiResponse
            {
                ErrorCode = RgfAiErrorCodes.AiProviderNotConfigured, Message = "Hidden server detail"
            })
        });
        var cut = context.Render<RgfAiChatComponent>(p => p
            .Add(c => c.Session, session).Add(c => c.ShowSendErrors, showSendErrors));
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        var alert = Assert.Single(cut.FindAll("[role=alert]"));
        Assert.Equal("configuration", alert.GetAttribute("data-error-kind"));
        Assert.Contains("alert-warning", alert.ClassList);
        Assert.Equal("No AI provider is configured. Configure an AI provider and model to use Recroby.", alert.TextContent);
        Assert.DoesNotContain("Hidden server detail", cut.Markup);
        Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
    }

    [Theory]
    [InlineData("AiCredit.Unknown")]
    [InlineData("aicredit.UserDisabled")]
    [InlineData("AiCredit.UserDisabled ")]
    [InlineData("AiCredit.UserDisabled.Internal")]
    [InlineData("InternalFailure")]
    [InlineData(null)]
    [InlineData("")]
    public async Task UnknownCodesHideServerDetailsAndRespectShowSendErrors(string? code)
    {
        foreach (var showSendErrors in new[] { false, true })
        {
            using var context = CreateContext();
            var session = new RgfAiConversationSession(new TestTransport
            {
                Send = _ => Task.FromResult(new RgfAiResponse { ErrorCode = code, Message = "Hidden server detail" })
            });
            var cut = context.Render<RgfAiChatComponent>(p => p
                .Add(c => c.Session, session).Add(c => c.ShowSendErrors, showSendErrors));
            cut.Find("textarea").Change("Hello");

            await cut.Find("button").ClickAsync(new MouseEventArgs());

            Assert.DoesNotContain("Hidden server detail", cut.Markup);
            Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
            if (showSendErrors)
            {
                var alert = Assert.Single(cut.FindAll("[role=alert]"));
                Assert.Equal("workflow", alert.GetAttribute("data-error-kind"));
                Assert.Contains("alert-warning", alert.ClassList);
                Assert.Equal("The Recroby workflow could not complete the request.", alert.TextContent);
            }
            else Assert.Empty(cut.FindAll("[role=alert]"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n ")]
    public async Task KnownCreditCodeWithMissingMessageShowsSafeFallback(string? message)
    {
        using var context = CreateContext();
        var session = new RgfAiConversationSession(new TestTransport
        {
            Send = _ => Task.FromResult(new RgfAiResponse { ErrorCode = "AiCredit.UserDisabled", Message = message! })
        });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Equal("The Recroby workflow could not complete the request.",
            cut.Find("[data-error-kind=credit]").TextContent);
    }

    [Fact]
    public async Task CreditMessageIsRenderedAsText()
    {
        using var context = CreateContext();
        const string message = "AI Credit access is disabled for <strong>User</strong><script>alert('detail')</script>.";
        var session = new RgfAiConversationSession(new TestTransport
        {
            Send = _ => Task.FromResult(new RgfAiResponse { ErrorCode = "AiCredit.UserDisabled", Message = message })
        });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        var alert = cut.Find("[data-error-kind=credit]");
        Assert.Equal(message, alert.TextContent);
        Assert.Empty(alert.Children);
    }

    [Fact]
    public async Task SuccessfulResponseIgnoresErrorCodeAndClearsPreviousCreditAlert()
    {
        using var context = CreateContext();
        var response = new RgfAiResponse { ErrorCode = "AiCredit.UserDisabled", Message = "Credit denied" };
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => Task.FromResult(response) });
        var cut = context.Render<RgfAiChatComponent>(p => p.Add(c => c.Session, session));
        cut.Find("textarea").Change("First turn");
        await cut.Find("button").ClickAsync(new MouseEventArgs());
        Assert.Single(cut.FindAll("[data-error-kind=credit]"));
        response = new RgfAiResponse { Success = true, ErrorCode = "AiCredit.UserDisabled", Message = "Answer" };
        cut.Find("textarea").Change("Second turn");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll("[role=alert]"));
        Assert.Equal("Answer", cut.Find(".message.text-bg-secondary").TextContent.Trim());
        Assert.Equal(3, session.Conversation.Messages.Count);
        Assert.Same(response, session.Conversation.Messages[2]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task TransportAndCancellationErrorsRetainVisibilityMessagesAndCallbacks(bool cancelled, bool showSendErrors)
    {
        using var context = CreateContext();
        Exception error = cancelled ? new OperationCanceledException("Hidden detail") : new HttpRequestException("Hidden detail");
        var failures = new List<Exception>();
        var session = new RgfAiConversationSession(new TestTransport { Send = _ => Task.FromException<RgfAiResponse>(error) });
        var cut = context.Render<RgfAiChatComponent>(p => p
            .Add(c => c.Session, session).Add(c => c.ShowSendErrors, showSendErrors)
            .Add(c => c.SendFailed, (Exception exception) => failures.Add(exception)));
        cut.Find("textarea").Change("Hello");

        await cut.Find("button").ClickAsync(new MouseEventArgs());

        Assert.Same(error, Assert.Single(failures));
        Assert.False(session.IsProcessing);
        Assert.Equal(RgfAiMessageRole.User, Assert.Single(session.Conversation.Messages).Role);
        Assert.DoesNotContain("Hidden detail", cut.Markup);
        if (showSendErrors)
        {
            var alert = Assert.Single(cut.FindAll("[role=alert]"));
            Assert.Equal(cancelled ? "cancelled" : "transport", alert.GetAttribute("data-error-kind"));
            Assert.Contains("alert-danger", alert.ClassList);
            Assert.Equal(cancelled ? "Request cancelled." :
                "The Recroby service could not be reached or returned an invalid response. Please try again.", alert.TextContent);
        }
        else Assert.Empty(cut.FindAll("[role=alert]"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IRecroDictService, FakeDashboardRecroDictService>();
        return context;
    }

    private static RgfAiResponse Response(string message) => new() { Success = true, Message = message };

    private sealed class TestTransport : IRgfAiTransport
    {
        public List<RgfAiRequest> Requests { get; } = [];
        public Func<RgfAiRequest, Task<RgfAiResponse>> Send { get; init; } = _ => Task.FromResult(Response("Answer"));

        public Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Send(request);
        }
    }
}
