using Recrovit.RecroGridFramework.Abstraction.Contracts.AI;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;
using Recrovit.RecroGridFramework.Client.Models;
using System.Text.Json;

namespace Recrovit.RecroGridFramework.Client.AI.Transport;

// RGF-DOC: rgf.client.ai.conversation-state
/// <summary>Adapts CustomFunction final progress to an AI response. The list handler owns progress lifecycle.</summary>
/// <remarks>Cancellation stops client-side waiting only; the server workflow and progress connection continue.</remarks>
public sealed class RgfAiCustomFunctionTransport : IRgfAiTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IRgListHandler listHandler;
    private readonly RgfAiCustomFunctionOptions options;
    private readonly RgfDynamicDictionary? rowData;

    public RgfAiCustomFunctionTransport(IRgListHandler listHandler, RgfAiCustomFunctionOptions options,
        RgfDynamicDictionary? rowData = null,
        Func<IDictionary<string, object>?>? additionalCustomParamsFactory = null,
        Func<RgfToastEventArgs?>? toastFactory = null)
    {
        ArgumentNullException.ThrowIfNull(listHandler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FunctionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.RequestParameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ResponseDataName);
        this.listHandler = listHandler;
        this.options = options;
        this.rowData = rowData;
        AdditionalCustomParamsFactory = additionalCustomParamsFactory;
        ToastFactory = toastFactory;
    }

    public Func<IDictionary<string, object>?>? AdditionalCustomParamsFactory { get; }

    public Func<RgfToastEventArgs?>? ToastFactory { get; }

    public async Task<RgfAiResponse> SendAsync(RgfAiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        RgfEntityKey? entityKey = null;
        if (rowData != null && (!listHandler.GetEntityKey(rowData, out entityKey) ||
            entityKey == null || entityKey.IsEmpty || string.IsNullOrWhiteSpace(entityKey.Signature)))
        {
            throw new InvalidOperationException("The explicit AI row context does not have a valid entity key.");
        }

        var customParams = new Dictionary<string, object>();
        var additionalParams = AdditionalCustomParamsFactory?.Invoke();
        if (additionalParams != null)
        {
            foreach (var parameter in additionalParams)
            {
                if (parameter.Key == options.RequestParameterName)
                {
                    throw new InvalidOperationException($"The additional CustomFunction parameters contain the reserved AI request key '{options.RequestParameterName}'.");
                }
                customParams.Add(parameter.Key, parameter.Value);
            }
        }
        customParams.Add(options.RequestParameterName, request);

        var completion = new TaskCompletionSource<RgfAiResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        var context = new RgfCustomFunctionContext
        {
            FunctionName = options.FunctionName,
            EntityKey = entityKey,
            CustomParams = customParams,
            Toast = ToastFactory?.Invoke(),
            EnableProgressTracking = true,
            ProgressChanged = (_, arg) => CompleteResponse(arg, completion)
        };

        // WaitAsync cancels only this wait, leaving invocation/progress ownership with the list handler.
        var result = await listHandler.CallCustomFunctionAsync(context).WaitAsync(cancellationToken);
        // Final progress can arrive before the invocation returns, even for a non-background result.
        if (completion.Task.IsCompleted)
        {
            return await completion.Task;
        }
        if (result == null || !result.Success)
        {
            throw new InvalidOperationException("The AI CustomFunction invocation failed.");
        }
        if (result.Result?.StartedInBackground != true)
        {
            throw new InvalidOperationException("The AI CustomFunction did not start in the background or supply a final response.");
        }
        return await completion.Task;
    }

    private void CompleteResponse(IRgfProgressArgs arg, TaskCompletionSource<RgfAiResponse> completion)
    {
        if (arg.IsBackgroundTaskCompleted != true || completion.Task.IsCompleted)
        {
            return;
        }
        try
        {
            if (arg.CustomData?.TryGetValue(options.ResponseDataName, out var data) != true || data == null)
            {
                throw new InvalidOperationException("Final AI progress did not contain a response.");
            }
            var json = data is string text ? text : JsonSerializer.Serialize(data, JsonOptions);
            var response = JsonSerializer.Deserialize<RgfAiResponse>(json, JsonOptions)
                ?? throw new InvalidOperationException("Final AI progress contained a null response.");
            completion.TrySetResult(response);
        }
        catch (Exception exception)
        {
            completion.TrySetException(new InvalidOperationException("Invalid final AI response protocol.", exception));
        }
    }
}
