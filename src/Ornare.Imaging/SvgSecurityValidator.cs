using System;
using System.IO;
using System.Text;
using System.Xml;
using Ornare.Core;

namespace Ornare.Imaging;

public static class SvgSecurityValidator
{
    public const int MaxSvgSizeBytes = 5 * 1024 * 1024; // 5 MB cap

    private static readonly string[] DisallowedTokens =
    [
        "<!DOCTYPE",
        "<!ENTITY",
        "<SCRIPT",
        "<FOREIGNOBJECT",
        "JAVASCRIPT:",
        "VBSCRIPT:",
        "ONLOAD=",
        "ONERROR=",
        "ONCLICK=",
        "ONMOUSE",
        "FILE://"
    ];

    private static readonly string[] AllowedUriNamespaces =
    [
        "HTTP://WWW.W3.ORG/2000/SVG",
        "HTTP://WWW.W3.ORG/1999/XLINK",
        "HTTPS://WWW.W3.ORG/2000/SVG",
        "HTTPS://WWW.W3.ORG/1999/XLINK",
        "HTTP://WWW.W3.ORG/XML/1998/NAMESPACE"
    ];

    public static OperationResult Validate(byte[] svgBytes)
    {
        if (svgBytes == null || svgBytes.Length == 0)
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "SVG data is empty.");
        }

        if (svgBytes.Length > MaxSvgSizeBytes)
        {
            return OperationResult.Failure(ErrorCode.ImageExceedsLimits, "SVG exceeds maximum allowed size (5MB).");
        }

        string content;
        try
        {
            content = Encoding.UTF8.GetString(svgBytes);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "The SVG text encoding is not supported.", ex.ToString());
        }

        return Validate(content);
    }

    public static OperationResult Validate(string svgContent)
    {
        if (string.IsNullOrWhiteSpace(svgContent))
        {
            return OperationResult.Failure(ErrorCode.InvalidTarget, "SVG content is empty.");
        }

        string upper = svgContent.ToUpperInvariant();
        foreach (var token in DisallowedTokens)
        {
            if (upper.Contains(token, StringComparison.Ordinal))
            {
                return OperationResult.Failure(
                    ErrorCode.SvgSecurityViolation,
                    $"SVG contains disallowed secure-policy construct: '{token}'.");
            }
        }

        // Check for disallowed external URIs outside standard XML namespaces
        string sanitizedUpper = upper;
        foreach (var ns in AllowedUriNamespaces)
        {
            sanitizedUpper = sanitizedUpper.Replace(ns, string.Empty, StringComparison.Ordinal);
        }

        if (sanitizedUpper.Contains("HTTP://", StringComparison.Ordinal) ||
            sanitizedUpper.Contains("HTTPS://", StringComparison.Ordinal) ||
            sanitizedUpper.Contains("FTP://", StringComparison.Ordinal))
        {
            return OperationResult.Failure(
                ErrorCode.SvgSecurityViolation,
                "SVG contains disallowed external network URI reference.");
        }

        // Validate well-formed XML with strict secure reader settings
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = 5_000_000
        };

        try
        {
            using var stringReader = new StringReader(svgContent);
            using var reader = XmlReader.Create(stringReader, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string elName = reader.LocalName.ToUpperInvariant();
                    if (elName is "SCRIPT" or "FOREIGNOBJECT")
                    {
                        return OperationResult.Failure(
                            ErrorCode.SvgSecurityViolation,
                            $"Disallowed element <{reader.LocalName}> found.");
                    }

                    if (reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                        {
                            string attrName = reader.LocalName.ToLowerInvariant();
                            string attrVal = reader.Value.Trim();
                            if (attrName is "href" or "src")
                            {
                                if (!attrVal.StartsWith('#'))
                                {
                                    return OperationResult.Failure(
                                        ErrorCode.SvgSecurityViolation,
                                        $"Disallowed external reference in attribute '{reader.Name}': '{attrVal}'.");
                                }
                            }
                        }
                        reader.MoveToElement();
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "The SVG is not well-formed XML.", ex.ToString());
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ErrorCode.ImageCorrupted, "The SVG could not be parsed.", ex.ToString());
        }

        return OperationResult.Success();
    }
}
