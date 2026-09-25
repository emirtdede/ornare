namespace Ornare.Core;

public sealed record UserSettings
{
    public string Theme { get; init; } = "System"; // System, Light, Dark
    public string Language { get; init; } = "System"; // System, tr-TR, en-US
}
