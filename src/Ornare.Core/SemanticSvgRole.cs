using System;

namespace Ornare.Core;

public enum SemanticSvgRole
{
    None = 0,
    Base = 1,
    Light = 2,
    Highlight = 3,
    Shadow = 4,
    Deep = 5,
    Badge = 6
}

public static class SemanticSvgRoleExtensions
{
    public const string RoleAttribute = "data-ornare-role";

    public static string ToRoleName(this SemanticSvgRole role) => role switch
    {
        SemanticSvgRole.Base => "base",
        SemanticSvgRole.Light => "light",
        SemanticSvgRole.Highlight => "highlight",
        SemanticSvgRole.Shadow => "shadow",
        SemanticSvgRole.Deep => "deep",
        SemanticSvgRole.Badge => "badge",
        _ => string.Empty
    };

    public static SemanticSvgRole ParseRole(string? roleString)
    {
        if (string.IsNullOrWhiteSpace(roleString))
        {
            return SemanticSvgRole.None;
        }

        return roleString.Trim().ToLowerInvariant() switch
        {
            "base" => SemanticSvgRole.Base,
            "light" => SemanticSvgRole.Light,
            "highlight" => SemanticSvgRole.Highlight,
            "shadow" => SemanticSvgRole.Shadow,
            "deep" => SemanticSvgRole.Deep,
            "badge" => SemanticSvgRole.Badge,
            _ => SemanticSvgRole.None
        };
    }

    public static bool IsValidRole(string? roleString) => ParseRole(roleString) != SemanticSvgRole.None;
}
