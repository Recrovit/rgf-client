using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Contracts.API;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.AI.Transport;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Reflection;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.Tests.AI;

public sealed class RgfAiCustomFunctionTransportTests
{
    private static RgfAiCustomFunctionOptions Options => new()
    {
        FunctionName = "host-function", RequestParameterName = "host-request", ResponseDataName = "host-response"
    };

    [Theory]
    [InlineData("FunctionName", null)]
    [InlineData("FunctionName", "")]
    [InlineData("FunctionName", " ")]
    [InlineData("RequestParameterName", "")]
    [InlineData("RequestParameterName", null)]
    [InlineData("RequestParameterName", " ")]
    [InlineData("ResponseDataName", null)]
    [InlineData("ResponseDataName", "")]
    [InlineData("ResponseDataName", " ")]
    public void Configuration_RequiresAllNames(string name, string? value)
    {
        var options = new RgfAiCustomFunctionOptions
        {
            FunctionName = name == "FunctionName" ? value! : Options.FunctionName,
            RequestParameterName = name == "RequestParameterName" ? value! : Options.RequestParameterName,
            ResponseDataName = name == "ResponseDataName" ? value! : Options.ResponseDataName
        };
        Assert.ThrowsAny<ArgumentException>(() => new RgfAiCustomFunctionTransport(CreateHandler(), options));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TargetAndConfiguration_AreForwarded(bool rowScoped)
    {
        var handler = CreateHandler();
        var fake = (ListHandlerProxy)handler;
        var row = new RgfDynamicDictionary { ["__rgparams"] = new Dictionary<string, object> { ["keySign"] = "signature" } };
        var toast = RgfToastEventArgs.CreateActionEvent(null, "Host AI", "");
        fake.Call = context =>
        {
            Assert.Equal(Options.FunctionName, context.FunctionName);
            Assert.True(context.EnableProgressTracking);
            Assert.Null(context.ProgressChangedAsync);
            Assert.Same(toast, context.Toast);
            if (rowScoped)
            {
                Assert.Equal("signature", context.EntityKey!.Signature);
                Assert.Same(fake.Keys, context.EntityKey.Keys);
            }
            else Assert.Null(context.EntityKey);
            Emit(context, true, "{\"success\":true,\"message\":\"answer\"}");
            return Task.FromResult<RgfResult<RgfCustomFunctionResult>?>(Result());
        };
        var transport = new RgfAiCustomFunctionTransport(handler, Options, rowScoped ? row : null, toastFactory: () => toast);
        var request = new RgfAiRequest { CurrentUserMessage = "turn" };
        Assert.Equal("answer", (await transport.SendAsync(request, TestContext.Current.CancellationToken)).Message);
        Assert.Same(request, fake.Context!.CustomParams![Options.RequestParameterName]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidExplicitRow_DoesNotFallBackToSelection(bool emptyKey)
    {
        var handler = CreateHandler();
        var fake = (ListHandlerProxy)handler;
        var row = new RgfDynamicDictionary();
        if (emptyKey)
        {
            row["__rgparams"] = new Dictionary<string, object> { ["keySign"] = "signature" };
            fake.Keys.Clear();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RgfAiCustomFunctionTransport(handler, Options, row).SendAsync(new(), TestContext.Current.CancellationToken));
        Assert.Null(fake.Context);
    }

    [Fact]
    public async Task AdditionalParams_AreFreshForEverySendAndDoNotMutateFactoryDictionary()
    {
        var handler = CreateHandler();
        var count = 0;
        var parameters = new Dictionary<string, object>();
        ((ListHandlerProxy)handler).Call = context =>
        {
            Assert.Equal(count, context.CustomParams!["host-value"]);
            Emit(context, true, "{\"success\":true}");
            return Task.FromResult<RgfResult<RgfCustomFunctionResult>?>(Result());
        };
        var transport = new RgfAiCustomFunctionTransport(handler, Options, additionalCustomParamsFactory: () =>
        {
            parameters["host-value"] = ++count;
            return parameters;
        });
        await transport.SendAsync(new(), TestContext.Current.CancellationToken);
        await transport.SendAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(2, count);
        Assert.False(parameters.ContainsKey(Options.RequestParameterName));
    }

    [Fact]
    public async Task ReservedRequestKeyCollision_IsRejectedBeforeInvocation()
    {
        var handler = CreateHandler();
        var transport = new RgfAiCustomFunctionTransport(handler, Options, additionalCustomParamsFactory: () =>
            new Dictionary<string, object> { [Options.RequestParameterName] = "override" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.SendAsync(new(), TestContext.Current.CancellationToken));
        Assert.Null(((ListHandlerProxy)handler).Context);
    }

    [Fact]
    public async Task IntermediateResponse_IsIgnoredUntilFinalResponse()
    {
        var handler = CreateHandler();
        var transport = new RgfAiCustomFunctionTransport(handler, Options);
        var task = transport.SendAsync(new(), TestContext.Current.CancellationToken);
        var context = ((ListHandlerProxy)handler).Context!;
        Emit(context, false, "{\"success\":true,\"message\":\"intermediate\"}");
        Assert.False(task.IsCompleted);
        using var json = JsonDocument.Parse("{\"success\":true,\"message\":\"final\"}");
        Emit(context, true, json.RootElement);
        Assert.Equal("final", (await task).Message);
    }

    [Fact]
    public async Task FailedAiResponse_IsANormalFinalResponse()
    {
        var handler = CreateHandler();
        var task = new RgfAiCustomFunctionTransport(handler, Options).SendAsync(new(), TestContext.Current.CancellationToken);
        Emit(((ListHandlerProxy)handler).Context!, true, "{\"success\":false,\"message\":\"declined\"}");
        var response = await task;
        Assert.False(response.Success);
        Assert.Equal("declined", response.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task InvalidFinalResponse_IsProtocolFailureEvenAfterIntermediateResponse(string? json)
    {
        var handler = CreateHandler();
        var task = new RgfAiCustomFunctionTransport(handler, Options).SendAsync(new(), TestContext.Current.CancellationToken);
        var context = ((ListHandlerProxy)handler).Context!;
        Emit(context, false, "{\"success\":true}");
        Emit(context, true, json);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("failed")]
    [InlineData("not-background")]
    public async Task FinalProgressBeforeInvocationReturn_WinsOverImmediateResult(string resultKind)
    {
        var handler = CreateHandler();
        var release = new TaskCompletionSource<RgfResult<RgfCustomFunctionResult>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((ListHandlerProxy)handler).Call = context =>
        {
            Emit(context, true, "{\"success\":true,\"message\":\"final\"}");
            return release.Task;
        };
        var task = new RgfAiCustomFunctionTransport(handler, Options).SendAsync(new(), TestContext.Current.CancellationToken);
        Assert.False(task.IsCompleted);
        release.SetResult(resultKind == "null" ? null : Result(resultKind != "failed", false));
        Assert.Equal("final", (await task).Message);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("failed")]
    [InlineData("not-background")]
    public async Task ImmediateFailureWithoutCompletion_DoesNotWaitForProgress(string resultKind)
    {
        var handler = CreateHandler();
        ((ListHandlerProxy)handler).Call = _ => Task.FromResult<RgfResult<RgfCustomFunctionResult>?>(
            resultKind == "null" ? null : Result(resultKind != "failed", resultKind != "not-background"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RgfAiCustomFunctionTransport(handler, Options).SendAsync(new(), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_StopsWaitingWithoutOwningProgressOrCancelingInvocation(bool duringInvocation)
    {
        var handler = CreateHandler();
        var invocation = new TaskCompletionSource<RgfResult<RgfCustomFunctionResult>?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (duringInvocation) ((ListHandlerProxy)handler).Call = _ => invocation.Task;
        using var cancellation = new CancellationTokenSource();
        var task = new RgfAiCustomFunctionTransport(handler, Options).SendAsync(new(), cancellation.Token);
        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(invocation.Task.IsCanceled);
        // No progress service is supplied at all: the adapter must not resolve, start, background or dispose one.
        Emit(((ListHandlerProxy)handler).Context!, true, "{\"success\":true}");
        invocation.SetResult(Result());
    }

    private static void Emit(RgfCustomFunctionContext context, bool final, object? response) =>
        context.ProgressChanged!(null!, new RgfProgressArgs(final)
        {
            CustomData = response == null ? new() : new() { [Options.ResponseDataName] = response }
        });

    private static RgfResult<RgfCustomFunctionResult> Result(bool success = true, bool background = true) =>
        new() { Success = success, Result = new() { StartedInBackground = background } };

    private static IRgListHandler CreateHandler() => DispatchProxy.Create<IRgListHandler, ListHandlerProxy>();

    public class ListHandlerProxy : DispatchProxy
    {
        public RgfDynamicDictionary Keys { get; } = new() { ["id"] = 42 };
        public RgfCustomFunctionContext? Context { get; private set; }
        public Func<RgfCustomFunctionContext, Task<RgfResult<RgfCustomFunctionResult>?>> Call { get; set; } =
            _ => Task.FromResult<RgfResult<RgfCustomFunctionResult>?>(Result());

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IRgListHandler.GetEKey)) return Keys;
            if (targetMethod.Name == nameof(IRgListHandler.CallCustomFunctionAsync))
            {
                Context = (RgfCustomFunctionContext)args![0]!;
                return Call(Context);
            }
            throw new NotSupportedException(targetMethod.Name);
        }
    }
}
