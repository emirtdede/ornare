using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ornare.Core;

namespace Ornare.Imaging.Templates;

/// <summary>
/// Strict geometry, security, and semantic role validator for Ornare folder templates (F03).
/// </summary>
public static class FolderTemplateValidator
{
    private static readonly HashSet<string> DisallowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "text", "tspan", "image", "foreignObject", "animate", "animateTransform",
        "filter", "feGaussianBlur", "feDropShadow", "style", "audio", "video", "iframe"
    };

    private static readonly HashSet<string> RequiredRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "base", "light", "highlight", "shadow", "deep"
    };

    public static OperationResult<bool> Validate(string svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            return OperationResult.Failure<bool>(ErrorCode.InvalidTarget, "Template SVG content is empty.");
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(svgContent, LoadOptions.None);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<bool>(ErrorCode.ImageCorrupted, $"Malformed XML in template: {ex.Message}");
        }

        var root = doc.Root;
        if (root == null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Failure<bool>(ErrorCode.InvalidTarget, "Root element must be <svg>.");
        }

        // 1. Verify viewBox="0 0 256 256"
        var viewBoxAttr = root.Attribute("viewBox");
        if (viewBoxAttr == null || !string.Equals(viewBoxAttr.Value.Trim(), "0 0 256 256", StringComparison.Ordinal))
        {
            return OperationResult.Failure<bool>(ErrorCode.InvalidTarget, "Template must have viewBox=\"0 0 256 256\".");
        }

        // 2. Scan for disallowed elements
        var allElements = root.Descendants().ToList();
        foreach (var el in allElements)
        {
            string localName = el.Name.LocalName;
            if (DisallowedElements.Contains(localName))
            {
                return OperationResult.Failure<bool>(ErrorCode.UnsupportedImageFormat, $"Disallowed SVG element <{localName}> detected.");
            }

            // Check for event handler attributes
            foreach (var attr in el.Attributes())
            {
                if (attr.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult.Failure<bool>(ErrorCode.UnsupportedImageFormat, $"Scripting attribute '{attr.Name.LocalName}' detected.");
                }
            }
        }

        // 3. Scan for semantic roles
        var foundRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in allElements)
        {
            var roleAttr = el.Attribute("data-ornare-role");
            if (roleAttr != null)
            {
                string role = roleAttr.Value.Trim();
                if (!RequiredRoles.Contains(role))
                {
                    return OperationResult.Failure<bool>(ErrorCode.InvalidTarget, $"Unknown semantic role '{role}' detected.");
                }
                foundRoles.Add(role);
            }
        }

        foreach (string required in RequiredRoles)
        {
            if (!foundRoles.Contains(required))
            {
                return OperationResult.Failure<bool>(ErrorCode.InvalidTarget, $"Missing required semantic role '{required}'.");
            }
        }

        // 4. Safe boundary verification: all rect elements must fit within x/y 16..240
        foreach (var el in allElements.Where(e => string.Equals(e.Name.LocalName, "rect", StringComparison.OrdinalIgnoreCase)))
        {
            if (double.TryParse(el.Attribute("x")?.Value, out double x) &&
                double.TryParse(el.Attribute("y")?.Value, out double y) &&
                double.TryParse(el.Attribute("width")?.Value, out double w) &&
                double.TryParse(el.Attribute("height")?.Value, out double h))
            {
                if (x < 16 || y < 16 || (x + w) > 240 || (y + h) > 240)
                {
                    return OperationResult.Failure<bool>(ErrorCode.InvalidTarget,
                        $"Element <rect x=\"{x}\" y=\"{y}\" w=\"{w}\" h=\"{h}\"> extends outside 16px safe boundary.");
                }
            }
        }

        return OperationResult.Success(true);
    }
}
