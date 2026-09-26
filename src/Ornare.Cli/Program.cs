using System;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ornare.Cli;

public static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    private static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Graceful fallback
        }

        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

        var rootCommand = BuildCommandLine();

        bool isJsonRequested = args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));

        if (args.Length == 0)
        {
            if (isJsonRequested)
            {
                CliResponseFormatter.WriteGeneralError(true, "unknown", "InvalidArguments", "A command must be specified.");
                return CliExitCodes.InvalidArguments;
            }
            // Human mode: display help
            return await rootCommand.Parse("--help").InvokeAsync();
        }

        var parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            string firstError = parseResult.Errors[0].Message;
            CliResponseFormatter.WriteGeneralError(isJsonRequested, "unknown", "InvalidArguments", firstError);
            return CliExitCodes.InvalidArguments;
        }

        // If root command itself was invoked without a subcommand (e.g. `ornare --json`)
        if (parseResult.CommandResult.Command == rootCommand)
        {
            if (args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase) || args.Contains("-?"))
            {
                return await parseResult.InvokeAsync();
            }

            CliResponseFormatter.WriteGeneralError(isJsonRequested, "unknown", "InvalidArguments", "A subcommand must be specified.");
            return CliExitCodes.InvalidArguments;
        }

        return await parseResult.InvokeAsync();
    }

    public static RootCommand BuildCommandLine()
    {
        var root = new RootCommand("Ornare CLI — Durable Windows folder icons and vector rendering (F24)");
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit machine-readable JSON output (locale-neutral schemaVersion 1)",
            Recursive = true
        };
        root.Add(jsonOption);

        // Subcommands
        root.Add(BuildApplyCommand(jsonOption));
        root.Add(BuildApplyLastCommand(jsonOption));
        root.Add(BuildResetCommand(jsonOption));
        root.Add(BuildUndoCommand(jsonOption));
        root.Add(BuildInspectCommand(jsonOption));
        root.Add(BuildExportIcoCommand(jsonOption));
        root.Add(BuildExportPngCommand(jsonOption));

        return root;
    }

    private static Command BuildApplyCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("apply", "Apply custom icon to target folder using render input");
        var folderArg = new Argument<string>("folder") { Description = "Path to target folder" };
        cmd.Add(folderArg);

        var (sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt) = AddRenderInputOptions(cmd);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string folder = result.GetValue(folderArg) ?? "";
            var renderInput = ExtractRenderInput(result, sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt);

            await using var handlers = new CliHandlers();
            return await handlers.HandleApplyAsync(folder, renderInput, isJson);
        });

        return cmd;
    }

    private static Command BuildApplyLastCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("apply-last", "Apply the most recently applied icon recipe to target folder");
        var folderArg = new Argument<string>("folder") { Description = "Path to target folder" };
        cmd.Add(folderArg);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string folder = result.GetValue(folderArg) ?? "";

            await using var handlers = new CliHandlers();
            return await handlers.HandleApplyLastAsync(folder, isJson);
        });

        return cmd;
    }

    private static Command BuildResetCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("reset", "Reset folder customization to clean uncustomized state");
        var folderArg = new Argument<string>("folder") { Description = "Path to target folder" };
        cmd.Add(folderArg);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string folder = result.GetValue(folderArg) ?? "";

            await using var handlers = new CliHandlers();
            return await handlers.HandleResetAsync(folder, isJson);
        });

        return cmd;
    }

    private static Command BuildUndoCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("undo", "Undo folder customization restoring prior icon semantics");
        var folderArg = new Argument<string>("folder") { Description = "Path to target folder" };
        cmd.Add(folderArg);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string folder = result.GetValue(folderArg) ?? "";

            await using var handlers = new CliHandlers();
            return await handlers.HandleUndoAsync(folder, isJson);
        });

        return cmd;
    }

    private static Command BuildInspectCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("inspect", "Inspect folder customization state, desktop.ini, and assignment");
        var folderArg = new Argument<string>("folder") { Description = "Path to target folder" };
        cmd.Add(folderArg);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string folder = result.GetValue(folderArg) ?? "";

            await using var handlers = new CliHandlers();
            return await handlers.HandleInspectAsync(folder, isJson);
        });

        return cmd;
    }

    private static Command BuildExportIcoCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("export-ico", "Export rendered multi-resolution Windows ICO file");
        var (sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt) = AddRenderInputOptions(cmd);

        var outputOpt = new Option<string>("--output")
        {
            Description = "Target output file path for generated .ico",
            Required = true
        };
        cmd.Add(outputOpt);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string outputPath = result.GetValue(outputOpt) ?? "";
            var renderInput = ExtractRenderInput(result, sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt);

            await using var handlers = new CliHandlers();
            return await handlers.HandleExportIcoAsync(outputPath, renderInput, isJson);
        });

        return cmd;
    }

    private static Command BuildExportPngCommand(Option<bool> jsonOption)
    {
        var cmd = new Command("export-png", "Export rendered high-resolution PNG file");
        var (sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt) = AddRenderInputOptions(cmd);

        var outputOpt = new Option<string>("--output")
        {
            Description = "Target output file path for generated .png",
            Required = true
        };
        cmd.Add(outputOpt);

        var sizeOpt = new Option<int>("--size")
        {
            Description = "Target dimension (256, 512, or 1024; default 1024)",
            DefaultValueFactory = _ => 1024
        };
        cmd.Add(sizeOpt);

        cmd.SetAction(async (ParseResult result, CancellationToken ct) =>
        {
            bool isJson = result.GetValue(jsonOption);
            string outputPath = result.GetValue(outputOpt) ?? "";
            int size = result.GetValue(sizeOpt);
            var renderInput = ExtractRenderInput(result, sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt);

            await using var handlers = new CliHandlers();
            return await handlers.HandleExportPngAsync(outputPath, size, renderInput, isJson);
        });

        return cmd;
    }

    private static (
        Option<string?> sourceOpt,
        Option<string?> colorOpt,
        Option<string?> presetOpt,
        Option<string?> templateOpt,
        Option<string?> modeOpt,
        Option<int?> paddingOpt,
        Option<string?> backgroundOpt,
        Option<string?> recolorOpt) AddRenderInputOptions(Command command)
    {
        var sourceOpt = new Option<string?>("--source") { Description = "Path to source image file (PNG, JPG, SVG, ICO)" };
        var colorOpt = new Option<string?>("--color") { Description = "Folder template base color hex (#RRGGBB)" };
        var presetOpt = new Option<string?>("--preset") { Description = "Stable work-status or priority preset identifier" };

        var templateOpt = new Option<string?>("--template") { Description = "Folder template: ornare-classic, ornare-soft, or ornare-flat" };
        var modeOpt = new Option<string?>("--mode") { Description = "Image placement mode: contain, cover, or fill" };
        var paddingOpt = new Option<int?>("--padding") { Description = "Padding percentage inside folder canvas (0..30)" };
        var backgroundOpt = new Option<string?>("--background") { Description = "Background color: transparent or #RRGGBB" };
        var recolorOpt = new Option<string?>("--recolor") { Description = "Target color hex (#RRGGBB) for single-color SVG recoloring" };

        command.Add(sourceOpt);
        command.Add(colorOpt);
        command.Add(presetOpt);
        command.Add(templateOpt);
        command.Add(modeOpt);
        command.Add(paddingOpt);
        command.Add(backgroundOpt);
        command.Add(recolorOpt);

        return (sourceOpt, colorOpt, presetOpt, templateOpt, modeOpt, paddingOpt, backgroundOpt, recolorOpt);
    }

    private static RenderInput ExtractRenderInput(
        ParseResult result,
        Option<string?> sourceOpt,
        Option<string?> colorOpt,
        Option<string?> presetOpt,
        Option<string?> templateOpt,
        Option<string?> modeOpt,
        Option<int?> paddingOpt,
        Option<string?> backgroundOpt,
        Option<string?> recolorOpt)
    {
        return new RenderInput
        {
            Source = result.GetValue(sourceOpt),
            Color = result.GetValue(colorOpt),
            Preset = result.GetValue(presetOpt),
            Template = result.GetValue(templateOpt),
            Mode = result.GetValue(modeOpt),
            Padding = result.GetValue(paddingOpt),
            Background = result.GetValue(backgroundOpt),
            Recolor = result.GetValue(recolorOpt),

            HasTemplate = result.GetResult(templateOpt) != null,
            HasMode = result.GetResult(modeOpt) != null,
            HasPadding = result.GetResult(paddingOpt) != null,
            HasBackground = result.GetResult(backgroundOpt) != null,
            HasRecolor = result.GetResult(recolorOpt) != null
        };
    }
}
