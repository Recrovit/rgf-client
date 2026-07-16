using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

#nullable enable

namespace Recrovit.RecroGridFramework.Abstraction.Contracts.API;

public class RgfUserState : ICloneable
{
    public RgfUserState() { }

    internal RgfUserState(RgfUserState source)
    {
        if (source != null)
        {
            IsValid = source.IsValid;
            IsAdmin = source.IsAdmin;
            Language = source.Language;
            UserName = source.UserName;
            IsNewlyCreated = source.IsNewlyCreated;
            Roles = CreateReadOnlyDictionary(source.Roles);
            Settings = CreateReadOnlyDictionary(source.Settings);
        }
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsValid { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsAdmin { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNewlyCreated { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Roles { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Settings { get; init; }

    public virtual object Clone() => DeepCopy(this)!;

    public static RgfUserState? DeepCopy(RgfUserState? source) => source == null ? null : new RgfUserState(source);

    private static IReadOnlyDictionary<string, string>? CreateReadOnlyDictionary(IReadOnlyDictionary<string, string>? source)
    {
        return source == null
            ? null
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(source, StringComparer.Ordinal));
    }
}

public static class RgfUserStateSettingKeys
{
    public const string Theme = "ui.theme";
    public const string Size = "ui.size";
    public const string Language = "ui.language";
}
