using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Ornare.Core;

namespace Ornare.Imaging;

public static class SvgThemeabilityAnalyzer
{
    private static readonly Dictionary<string, string> StandardNamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        { "black", "#000000" },
        { "white", "#FFFFFF" },
        { "red", "#FF0000" },
        { "green", "#008000" },
        { "blue", "#0000FF" },
        { "yellow", "#FFFF00" },
        { "cyan", "#00FFFF" },
        { "magenta", "#FF00FF" },
        { "gray", "#808080" },
        { "grey", "#808080" },
        { "silver", "#C0C0C0" },
        { "maroon", "#800000" },
        { "navy", "#000080" },
        { "olive", "#808000" },
        { "purple", "#800080" },
        { "teal", "#008080" }
    };

    public static OperationResult<string> AnalyzeSingleColorThemeability(string svgContent)
    {
        var secResult = SvgSecurityValidator.Validate(svgContent);
        if (!secResult.IsSuccess)
        {
            return OperationResult.Failure<string>(secResult.ErrorCode, secResult.ErrorMessage ?? "SVG security validation failed.");
        }

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 5_000_000
            };
            using var sr = new StringReader(svgContent);
            using var reader = XmlReader.Create(sr, settings);
            doc = XDocument.Load(reader);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<string>(ErrorCode.ImageCorrupted, $"Failed to parse SVG XML: {ex.Message}");
        }

        // Rule 1: No <style> tags (stylesheets require full CSS engine and are not themeable)
        if (doc.Descendants().Any(e => string.Equals(e.Name.LocalName, "style", StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult.Failure<string>(ErrorCode.SvgNotThemeable, "SVG contains <style> stylesheets.");
        }

        // Rule 2: No <pattern> elements
        if (doc.Descendants().Any(e => string.Equals(e.Name.LocalName, "pattern", StringComparison.OrdinalIgnoreCase)))
        {
            return OperationResult.Failure<string>(ErrorCode.SvgNotThemeable, "SVG contains <pattern> elements.");
        }

        // Rule 3: Gradients must have data-ornare-role on every stop, otherwise not single-color themeable
        var gradients = doc.Descendants().Where(e =>
            string.Equals(e.Name.LocalName, "linearGradient", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.Name.LocalName, "radialGradient", StringComparison.OrdinalIgnoreCase));

        foreach (var g in gradients)
        {
            var stops = g.Elements().Where(s => string.Equals(s.Name.LocalName, "stop", StringComparison.OrdinalIgnoreCase)).ToList();
            if (stops.Count == 0 || stops.Any(s => s.Attribute("data-ornare-role") == null))
            {
                return OperationResult.Failure<string>(ErrorCode.SvgNotThemeable, "SVG contains unannotated gradients.");
            }
        }

        var distinctColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Traverse all elements in document
        foreach (var el in doc.Descendants())
        {
            string name = el.Name.LocalName.ToLowerInvariant();

            // Skip non-rendering metadata elements
            if (name is "defs" or "metadata" or "clippath" or "mask")
            {
                continue;
            }

            // Check attributes and style declarations
            string? fill = el.Attribute("fill")?.Value;
            string? stroke = el.Attribute("stroke")?.Value;
            string? color = el.Attribute("color")?.Value;
            string? style = el.Attribute("style")?.Value;

            if (!string.IsNullOrEmpty(style))
            {
                ParseStyleDeclarations(style, out string? styleFill, out string? styleStroke, out string? styleColor);
                if (styleFill != null) fill = styleFill;
                if (styleStroke != null) stroke = styleStroke;
                if (styleColor != null) color = styleColor;
            }

            // Check for unsupported constructs
            if (ContainsUnsupportedPaint(fill) || ContainsUnsupportedPaint(stroke))
            {
                return OperationResult.Failure<string>(ErrorCode.SvgNotThemeable, "SVG uses unsupported paint servers or CSS variables.");
            }

            // Inherited color fallback
            string inheritedColor = FindInheritedColor(el) ?? "#000000";

            if (TryResolveSolidColor(fill, inheritedColor, out string resolvedFill))
            {
                distinctColors.Add(resolvedFill);
            }

            if (TryResolveSolidColor(stroke, inheritedColor, out string resolvedStroke))
            {
                distinctColors.Add(resolvedStroke);
            }
        }

        if (distinctColors.Count == 0)
        {
            // If an SVG has shapes with default fill (SVG default is black if unspecified)
            bool hasShapes = doc.Descendants().Any(e => e.Name.LocalName.ToLowerInvariant() is
                "path" or "rect" or "circle" or "ellipse" or "polygon" or "polyline" or "line" or "text");

            if (hasShapes)
            {
                return OperationResult.Success("#000000");
            }

            return OperationResult.Failure<string>(ErrorCode.SvgNotThemeable, "No renderable solid colors found in SVG.");
        }

        if (distinctColors.Count == 1)
        {
            return OperationResult.Success(distinctColors.First());
        }

        return OperationResult.Failure<string>(
            ErrorCode.SvgNotThemeable,
            $"SVG contains multiple distinct colors ({string.Join(", ", distinctColors)}). Only single-color SVGs are themeable.");
    }

    private static bool ContainsUnsupportedPaint(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return false;
        string lower = val.Trim().ToLowerInvariant();
        return lower.Contains("url(") || lower.Contains("var(");
    }

    private static void ParseStyleDeclarations(string style, out string? fill, out string? stroke, out string? color)
    {
        fill = null;
        stroke = null;
        color = null;

        var declarations = style.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var decl in declarations)
        {
            int colon = decl.IndexOf(':');
            if (colon <= 0) continue;

            string prop = decl[..colon].Trim().ToLowerInvariant();
            string val = decl[(colon + 1)..].Trim();

            if (prop == "fill") fill = val;
            else if (prop == "stroke") stroke = val;
            else if (prop == "color") color = val;
        }
    }

    private static string? FindInheritedColor(XElement element)
    {
        XElement? cur = element;
        while (cur != null)
        {
            string? colorAttr = cur.Attribute("color")?.Value;
            if (!string.IsNullOrWhiteSpace(colorAttr) && TryNormalizeColor(colorAttr, "#000000", out string normalized))
            {
                return normalized;
            }

            string? style = cur.Attribute("style")?.Value;
            if (!string.IsNullOrEmpty(style))
            {
                ParseStyleDeclarations(style, out _, out _, out string? styleColor);
                if (!string.IsNullOrWhiteSpace(styleColor) && TryNormalizeColor(styleColor, "#000000", out string normStyleColor))
                {
                    return normStyleColor;
                }
            }

            cur = cur.Parent;
        }
        return null;
    }

    private static bool TryResolveSolidColor(string? paintValue, string inheritedColor, out string resolvedColor)
    {
        resolvedColor = string.Empty;
        if (string.IsNullOrWhiteSpace(paintValue))
        {
            return false;
        }

        string trimmed = paintValue.Trim();
        if (string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(trimmed, "currentColor", StringComparison.OrdinalIgnoreCase))
        {
            resolvedColor = inheritedColor;
            return true;
        }

        return TryNormalizeColor(trimmed, inheritedColor, out resolvedColor);
    }

    public static bool TryNormalizeColor(string input, string currentColorFallback, out string normalizedHex)
    {
        normalizedHex = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        string trimmed = input.Trim().ToLowerInvariant();

        if (trimmed == "currentcolor")
        {
            normalizedHex = currentColorFallback;
            return true;
        }

        if (StandardNamedColors.TryGetValue(trimmed, out string? namedHex))
        {
            normalizedHex = namedHex;
            return true;
        }

        if (SemanticColorPalette.TryNormalizeHexColor(trimmed, out string hex))
        {
            // Truncate to 6 digits RGB for single-color comparison
            normalizedHex = hex.Length == 9 ? hex[..7] : hex;
            return true;
        }

        // rgb(r, g, b)
        var rgbMatch = Regex.Match(trimmed, @"^rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$");
        if (rgbMatch.Success)
        {
            int r = int.Parse(rgbMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            int g = int.Parse(rgbMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            int b = int.Parse(rgbMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            if (r <= 255 && g <= 255 && b <= 255)
            {
                normalizedHex = $"#{r:X2}{g:X2}{b:X2}";
                return true;
            }
        }

        return false;
    }
}
