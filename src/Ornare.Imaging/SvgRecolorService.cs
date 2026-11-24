using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ornare.Core;

namespace Ornare.Imaging;

public static class SvgRecolorService
{
    public static OperationResult<string> RecolorSemanticTemplate(string svgContent, SemanticColorPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var sec = SvgSecurityValidator.Validate(svgContent);
        if (!sec.IsSuccess)
        {
            return OperationResult.Failure<string>(sec.ErrorCode, sec.ErrorMessage ?? "SVG security check failed.");
        }

        XDocument doc;
        try
        {
            doc = LoadSecureXml(svgContent);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<string>(ErrorCode.ImageCorrupted, "The SVG could not be parsed.", ex.ToString());
        }

        bool anyRoleFound = false;

        foreach (var el in doc.Descendants())
        {
            var roleAttr = el.Attribute(SemanticSvgRoleExtensions.RoleAttribute) ?? el.Attribute(SemanticSvgRoleExtensions.LegacyRoleAttribute);
            if (roleAttr == null || string.IsNullOrWhiteSpace(roleAttr.Value))
            {
                continue;
            }

            var role = SemanticSvgRoleExtensions.ParseRole(roleAttr.Value);
            if (role == SemanticSvgRole.None)
            {
                continue;
            }

            anyRoleFound = true;
            string targetColor = palette.GetColor(role);
            string localName = el.Name.LocalName.ToLowerInvariant();

            if (localName == "stop")
            {
                // Gradient stop element
                el.SetAttributeValue("stop-color", targetColor);
                UpdateStyleProperty(el, "stop-color", targetColor);
            }
            else
            {
                // Shape or group element
                bool hasFill = el.Attribute("fill") != null;
                bool hasStroke = el.Attribute("stroke") != null;
                bool hasStyle = el.Attribute("style") != null;

                if (hasFill)
                {
                    string fillVal = el.Attribute("fill")!.Value.Trim();
                    if (!string.Equals(fillVal, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        el.SetAttributeValue("fill", targetColor);
                    }
                }

                if (hasStroke)
                {
                    string strokeVal = el.Attribute("stroke")!.Value.Trim();
                    if (!string.Equals(strokeVal, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        el.SetAttributeValue("stroke", targetColor);
                    }
                }

                if (hasStyle)
                {
                    UpdateStyleProperty(el, "fill", targetColor);
                    UpdateStyleProperty(el, "stroke", targetColor);
                }

                if (!hasFill && !hasStroke && !hasStyle && localName is "path" or "rect" or "circle" or "polygon" or "polyline")
                {
                    el.SetAttributeValue("fill", targetColor);
                }
            }
        }

        if (!anyRoleFound)
        {
            return OperationResult.Failure<string>(
                ErrorCode.SvgNotThemeable,
                $"Template contains no '{SemanticSvgRoleExtensions.RoleAttribute}' semantic annotations.");
        }

        return OperationResult.Success(SerializeXml(doc));
    }

    public static OperationResult<string> RecolorUserSingleColorSvg(string svgContent, string targetHexColor)
    {
        if (!SemanticColorPalette.TryNormalizeHexColor(targetHexColor, out string normalizedTarget))
        {
            return OperationResult.Failure<string>(
                ErrorCode.InvalidTarget, $"Target hex color '{targetHexColor}' is invalid.");
        }

        var analysis = SvgThemeabilityAnalyzer.AnalyzeSingleColorThemeability(svgContent);
        if (!analysis.IsSuccess)
        {
            return OperationResult.Failure<string>(analysis.ErrorCode, analysis.ErrorMessage ?? "SVG is not single-color themeable.");
        }

        XDocument doc;
        try
        {
            doc = LoadSecureXml(svgContent);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure<string>(ErrorCode.ImageCorrupted, "The SVG could not be parsed.", ex.ToString());
        }

        foreach (var el in doc.Descendants())
        {
            string localName = el.Name.LocalName.ToLowerInvariant();
            if (localName is "defs" or "metadata" or "clippath" or "mask")
            {
                continue;
            }

            var fillAttr = el.Attribute("fill");
            if (fillAttr != null)
            {
                string f = fillAttr.Value.Trim();
                if (!string.Equals(f, "none", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(f, "transparent", StringComparison.OrdinalIgnoreCase))
                {
                    fillAttr.Value = normalizedTarget;
                }
            }

            var strokeAttr = el.Attribute("stroke");
            if (strokeAttr != null)
            {
                string s = strokeAttr.Value.Trim();
                if (!string.Equals(s, "none", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(s, "transparent", StringComparison.OrdinalIgnoreCase))
                {
                    strokeAttr.Value = normalizedTarget;
                }
            }

            var colorAttr = el.Attribute("color");
            if (colorAttr != null)
            {
                colorAttr.Value = normalizedTarget;
            }

            var styleAttr = el.Attribute("style");
            if (styleAttr != null && !string.IsNullOrEmpty(styleAttr.Value))
            {
                UpdateStyleProperty(el, "fill", normalizedTarget);
                UpdateStyleProperty(el, "stroke", normalizedTarget);
                UpdateStyleProperty(el, "color", normalizedTarget);
            }

            // If a shape has no fill or stroke specified, SVG standard defaults fill to black
            if (fillAttr == null && strokeAttr == null && styleAttr == null &&
                localName is "path" or "rect" or "circle" or "ellipse" or "polygon" or "polyline")
            {
                el.SetAttributeValue("fill", normalizedTarget);
            }
        }

        return OperationResult.Success(SerializeXml(doc));
    }

    private static XDocument LoadSecureXml(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 5_000_000
        };
        using var sr = new StringReader(xml);
        using var reader = XmlReader.Create(sr, settings);
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static string SerializeXml(XDocument doc)
    {
        var sb = new StringBuilder();
        var xmlSettings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = false,
            Encoding = Encoding.UTF8
        };
        using (var writer = XmlWriter.Create(sb, xmlSettings))
        {
            doc.Save(writer);
        }
        return sb.ToString();
    }

    private static void UpdateStyleProperty(XElement element, string propertyName, string newColor)
    {
        var styleAttr = element.Attribute("style");
        if (styleAttr == null || string.IsNullOrWhiteSpace(styleAttr.Value))
        {
            return;
        }

        var parts = styleAttr.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool modified = false;

        for (int i = 0; i < parts.Length; i++)
        {
            int colon = parts[i].IndexOf(':');
            if (colon <= 0) continue;

            string prop = parts[i][..colon].Trim().ToLowerInvariant();
            string val = parts[i][(colon + 1)..].Trim();

            if (prop == propertyName &&
                !string.Equals(val, "none", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(val, "transparent", StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"{prop}:{newColor}";
                modified = true;
            }
        }

        if (modified)
        {
            styleAttr.Value = string.Join("; ", parts);
        }
    }
}
