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
    Badge = 6,
    FolderBack = 7,
    FolderTab = 8,
    FolderFront = 9,
    Edge = 10,
    Detail = 11
}

public static class SemanticSvgRoleExtensions
{
    public const string RoleAttribute = "data-role";
    public const string LegacyRoleAttribute = "data-ornare-role";

    public static string ToRoleName(this SemanticSvgRole role) => role switch
    {
        SemanticSvgRole.Base => "base",
        SemanticSvgRole.Light => "light",
        SemanticSvgRole.Highlight => "highlight",
        SemanticSvgRole.Shadow => "shadow",
        SemanticSvgRole.Deep => "deep",
        SemanticSvgRole.Badge => "badge",
        SemanticSvgRole.FolderBack => "folder-back",
        SemanticSvgRole.FolderTab => "folder-tab",
        SemanticSvgRole.FolderFront => "folder-front",
        SemanticSvgRole.Edge => "edge",
        SemanticSvgRole.Detail => "detail",
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
            "base" or "folder-front" or "front" => SemanticSvgRole.FolderFront,
            "deep" or "folder-back" or "back" => SemanticSvgRole.FolderBack,
            "folder-tab" or "tab" => SemanticSvgRole.FolderTab,
            "highlight" => SemanticSvgRole.Highlight,
            "shadow" => SemanticSvgRole.Shadow,
            "light" or "edge" => SemanticSvgRole.Edge,
            "detail" => SemanticSvgRole.Detail,
            "badge" => SemanticSvgRole.Badge,
            _ => SemanticSvgRole.None
        };
    }

    public static bool IsValidRole(string? roleString) => ParseRole(roleString) != SemanticSvgRole.None;
}
