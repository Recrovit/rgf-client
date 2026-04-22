using Microsoft.JSInterop;

namespace Recrovit.RecroGridFramework.Client.Blazor.UI.Tests.Testing;

internal sealed class RecordingJsRuntime : IJSRuntime
{
    private readonly FakeJsObjectReference _objectReference = new();

    public List<InvocationRecord> Invocations { get; } = [];

    public int JQueryUiVersionComparisonResult { get; set; } = -1;

    public bool EvalBooleanResult { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Invocations.Add(new(identifier, args ?? []));

        object? result = identifier switch
        {
            "Recrovit.LPUtils.CompareJQueryUIVersion" => JQueryUiVersionComparisonResult,
            "import" => _objectReference,
            "Recrovit.LPUtils.AddStyleSheetLink" => true,
            "Recrovit.LPUtils.EnsureStyleSheetLoaded" => true,
            "eval" when typeof(TValue) == typeof(bool) => EvalBooleanResult,
            _ when typeof(TValue) == typeof(bool) => true,
            _ => GetDefaultValue(typeof(TValue)),
        };

        return new ValueTask<TValue>((TValue)result!);
    }

    public IReadOnlyList<InvocationRecord> GetInvocations(string identifier)
        => Invocations.Where(invocation => invocation.Identifier == identifier).ToArray();

    private static object? GetDefaultValue(Type type)
    {
        if (type == typeof(object))
        {
            return new object();
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    internal sealed record InvocationRecord(string Identifier, IReadOnlyList<object?> Arguments);

    private sealed class FakeJsObjectReference : IJSObjectReference
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => new(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => new(default(TValue)!);
    }
}
