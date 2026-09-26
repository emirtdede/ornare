using System;
using System.IO;
using System.Linq;
using Ornare.Core;
using Ornare.Core.Presets;
using Ornare.Imaging;

namespace Ornare.Cli;

/// <summary>
/// Validates the Render Input mutex and modifier rules (F24).
/// Fixed rules:
/// - Exactly one of --source, --color, --preset is required.
/// - --template valid only with --color (default: ornare-classic).
/// - --mode, --padding, --background valid only with --source (defaults: contain, 0, transparent).
/// - --recolor valid only with SVG --source.
/// - --preset accepts no render modifiers.
/// </summary>
public static class RenderInputValidator
{
    private static readonly string[] AllowedTemplates = ["ornare-classic", "ornare-soft", "ornare-flat"];
    private static readonly string[] AllowedModes = ["contain", "cover", "fill"];

    public static RenderValidationResult Validate(RenderInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        bool hasSource = !string.IsNullOrWhiteSpace(input.Source);
        bool hasColor = !string.IsNullOrWhiteSpace(input.Color);
        bool hasPreset = !string.IsNullOrWhiteSpace(input.Preset);

        int count = (hasSource ? 1 : 0) + (hasColor ? 1 : 0) + (hasPreset ? 1 : 0);
        if (count == 0)
        {
            return RenderValidationResult.Failure(
                CliExitCodes.InvalidArguments,
                "InvalidArguments",
                "Exactly one render input (--source, --color, or --preset) must be specified.");
        }

        if (count > 1)
        {
            return RenderValidationResult.Failure(
                CliExitCodes.InvalidArguments,
                "InvalidArguments",
                "Conflicting render inputs: only one of --source, --color, or --preset may be specified.");
        }

        if (hasPreset)
        {
            if (input.HasTemplate || input.HasMode || input.HasPadding || input.HasBackground || input.HasRecolor)
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    "Render modifiers (--template, --mode, --padding, --background, --recolor) are not permitted with --preset.");
            }

            string presetId = input.Preset!.Trim();
            if (!StatusPresetCatalog.TryGetPreset(presetId, 1, out var preset) || preset == null)
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    $"Unknown preset ID '{presetId}'. Valid presets are: {string.Join(", ", StatusPresetCatalog.All.Select(p => p.Id))}.");
            }

            return RenderValidationResult.Success(new ValidatedRenderInput
            {
                Kind = RenderInputKind.Preset,
                Preset = preset
            });
        }

        if (hasColor)
        {
            if (input.HasMode || input.HasPadding || input.HasBackground || input.HasRecolor)
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    "Render modifiers (--mode, --padding, --background, --recolor) are only permitted with --source, not with --color.");
            }

            string colorStr = input.Color!.Trim();
            if (!ColorConversion.TryParseHex(colorStr, out byte r, out byte g, out byte b))
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    $"Invalid color hex '{colorStr}'. Expected format #RRGGBB.");
            }

            string templateId = "ornare-classic";
            if (input.HasTemplate && !string.IsNullOrWhiteSpace(input.Template))
            {
                string rawTemplate = input.Template.Trim().ToLowerInvariant();
                if (!AllowedTemplates.Contains(rawTemplate))
                {
                    return RenderValidationResult.Failure(
                        CliExitCodes.InvalidArguments,
                        "InvalidArguments",
                        $"Invalid template '{input.Template}'. Valid templates are: {string.Join(", ", AllowedTemplates)}.");
                }
                templateId = rawTemplate;
            }

            return RenderValidationResult.Success(new ValidatedRenderInput
            {
                Kind = RenderInputKind.Color,
                ColorHex = ColorConversion.ToHex(r, g, b),
                TemplateId = templateId
            });
        }

        // hasSource
        if (input.HasTemplate)
        {
            return RenderValidationResult.Failure(
                CliExitCodes.InvalidArguments,
                "InvalidArguments",
                "Modifier --template is only permitted with --color, not with --source.");
        }

        string sourcePath = input.Source!.Trim();
        string fullSourcePath;
        try
        {
            fullSourcePath = Path.GetFullPath(sourcePath);
        }
        catch (Exception ex)
        {
            return RenderValidationResult.Failure(
                CliExitCodes.InvalidArguments,
                "InvalidArguments",
                $"Invalid source path format: {ex.Message}");
        }

        if (!File.Exists(fullSourcePath))
        {
            return RenderValidationResult.Failure(
                CliExitCodes.UnsupportedTarget,
                "TargetNotFound",
                $"Source image file not found: '{fullSourcePath}'.",
                ErrorCode.TargetNotFound);
        }

        ImagePlacementMode placementMode = ImagePlacementMode.Contain;
        if (input.HasMode && !string.IsNullOrWhiteSpace(input.Mode))
        {
            string modeStr = input.Mode.Trim().ToLowerInvariant();
            placementMode = modeStr switch
            {
                "contain" => ImagePlacementMode.Contain,
                "cover" => ImagePlacementMode.Cover,
                "fill" => ImagePlacementMode.Fill,
                _ => (ImagePlacementMode)(-1)
            };

            if ((int)placementMode == -1)
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    $"Invalid mode '{input.Mode}'. Valid modes are: {string.Join(", ", AllowedModes)}.");
            }
        }

        int padding = 0;
        if (input.HasPadding && input.Padding.HasValue)
        {
            if (input.Padding.Value < 0 || input.Padding.Value > 30)
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    $"Padding percentage must be between 0 and 30 (received {input.Padding.Value}).");
            }
            padding = input.Padding.Value;
        }

        BackgroundMode backgroundMode = BackgroundMode.Transparent;
        string? backgroundHex = null;
        if (input.HasBackground && !string.IsNullOrWhiteSpace(input.Background))
        {
            string bgStr = input.Background.Trim();
            if (string.Equals(bgStr, "transparent", StringComparison.OrdinalIgnoreCase))
            {
                backgroundMode = BackgroundMode.Transparent;
            }
            else
            {
                if (!ColorConversion.TryParseHex(bgStr, out byte bgR, out byte bgG, out byte bgB))
                {
                    return RenderValidationResult.Failure(
                        CliExitCodes.InvalidArguments,
                        "InvalidArguments",
                        $"Invalid background color '{bgStr}'. Expected 'transparent' or #RRGGBB.");
                }
                backgroundMode = BackgroundMode.SolidColor;
                backgroundHex = ColorConversion.ToHex(bgR, bgG, bgB);
            }
        }

        string? recolorHex = null;
        if (input.HasRecolor && !string.IsNullOrWhiteSpace(input.Recolor))
        {
            string ext = Path.GetExtension(fullSourcePath);
            if (!string.Equals(ext, ".svg", StringComparison.OrdinalIgnoreCase))
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    "Modifier --recolor is valid only with SVG sources.");
            }

            string recStr = input.Recolor.Trim();
            if (!ColorConversion.TryParseHex(recStr, out byte recR, out byte recG, out byte recB))
            {
                return RenderValidationResult.Failure(
                    CliExitCodes.InvalidArguments,
                    "InvalidArguments",
                    $"Invalid recolor hex '{recStr}'. Expected format #RRGGBB.");
            }
            recolorHex = ColorConversion.ToHex(recR, recG, recB);
        }

        return RenderValidationResult.Success(new ValidatedRenderInput
        {
            Kind = RenderInputKind.Source,
            SourcePath = fullSourcePath,
            Mode = placementMode,
            Padding = padding,
            BackgroundMode = backgroundMode,
            BackgroundColorHex = backgroundHex,
            RecolorHex = recolorHex
        });
    }
}
