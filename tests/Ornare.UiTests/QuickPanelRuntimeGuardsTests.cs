using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Ornare.UiTests;

/// <summary>
/// Regression guards for the defects found in the pre-deployment audit: a shell extension that
/// could terminate explorer.exe, a staging path with no real reentrancy lock, an unbounded image
/// read, a preview race, and window handlers that kept writing to a closed visual tree.
/// <para>
/// These read source rather than run the UI, because the failing conditions (two concurrent
/// drag-drops, a 2 GB "image", a disposed window) cannot be produced reliably in a unit test.
/// A source guard still fails the build the day someone deletes the protection.
/// </para>
/// </summary>
public class QuickPanelRuntimeGuardsTests
{
    private static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Ornare.slnx")))
            {
                return current;
            }

            current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException("Could not find the repository root from " + AppContext.BaseDirectory);
    }

    private static string ReadAllQuickPanelPartials()
    {
        string appDir = Path.Combine(FindRepositoryRoot(), "src", "Ornare.App");
        var files = Directory.EnumerateFiles(appDir, "QuickPanelWindow*.cs").OrderBy(p => p, StringComparer.Ordinal).ToList();
        Assert.True(files.Count > 0, $"QuickPanelWindow partials not found in {appDir}");
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    [Fact]
    public void StagingIsGuardedByAnInterlockedGateThatIsAlwaysReleased()
    {
        string code = ReadAllQuickPanelPartials();

        // The gate must be a real atomic counter, not the derived UI flag.
        Assert.Contains("Interlocked.CompareExchange(ref _stagingGate, 1, 0)", code);
        Assert.Contains("Interlocked.Exchange(ref _stagingGate, 0)", code);

        // Every staging entry point must consult it, and every acquisition must be paired with a
        // finally release.
        int acquisitions = CountOccurrences(code, "if (!TryBeginStaging())");
        int releases = CountOccurrences(code, "EndStaging();");
        Assert.True(acquisitions > 0, "No staging entry point calls TryBeginStaging().");
        Assert.True(releases >= acquisitions, $"TryBeginStaging ({acquisitions}) has fewer EndStaging ({releases}) releases.");

        // The two staging entry points that read a payload must refuse an oversized one.
        Assert.Contains("OrnareConstants.MaxInputImageBytes", code);
    }

    [Fact]
    public void PreviewRendersCannotOverwriteANewerRender()
    {
        string code = ReadAllQuickPanelPartials();

        Assert.Contains("Interlocked.Increment(ref _previewRenderSequence)", code);
        Assert.Contains("IsStalePreview(sequence)", code);

        // Every assignment of the preview bitmap must be preceded by a staleness check, so a
        // slow render cannot paint over a newer one.
        int assignments = CountOccurrences(code, "PreviewIconImage.Source = LoadPreviewBitmapFromBytes(");
        int staleChecks = CountOccurrences(code, "if (IsStalePreview(sequence))");
        Assert.True(staleChecks >= assignments, $"Preview assignments ({assignments}) outnumber staleness checks ({staleChecks}).");
    }

    [Fact]
    public void ClosedPanelStopsTouchingTheVisualTree()
    {
        string code = ReadAllQuickPanelPartials();

        // Dispose sets the flag before releasing anything else.
        Assert.Contains("_disposed = true;", code);

        // The guard must not surface an error onto a disconnected tree.
        Assert.Contains("if (!_disposed)", code);
    }

    [Fact]
    public void LanguageChangesReachAnOpenPanelAndTheSubscriptionIsReleased()
    {
        string code = ReadAllQuickPanelPartials();

        Assert.Contains("AppResources.CurrentLocaleChanged += OnAppLanguageChanged;", code);
        Assert.Contains("AppResources.CurrentLocaleChanged -= OnAppLanguageChanged;", code);
    }

    [Fact]
    public void ErrorCopyChannelsAreTypeSeparated()
    {
        string code = ReadAllQuickPanelPartials();

        // The old signature took two strings, so an exception message could be passed into the
        // rendered slot and compile. The record struct removes that possibility.
        Assert.Contains("QuickPanelErrorCopy", code);
        Assert.DoesNotContain("ShowCategorizedError(QuickPanelErrorType errorType, string?", code);
        Assert.Contains("QuickPanelErrorCopy.Diagnostic(", code);
        Assert.Contains("QuickPanelErrorCopy.Message(", code);
    }

    [Fact]
    public void ShellExtensionCatchesEveryExceptionBeforeItCanReachExplorer()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "Ornare.ShellExtension", "dllmain.cpp"));

        // Invoke runs on Explorer's UI thread inside a noexcept method: without a catch-all a
        // std::bad_alloc calls std::terminate inside explorer.exe.
        int invokeIndex = source.IndexOf("IFACEMETHODIMP Invoke(", StringComparison.Ordinal);
        Assert.True(invokeIndex >= 0, "Invoke was not found in dllmain.cpp.");
        string invokeBody = source.Substring(invokeIndex, Math.Min(1200, source.Length - invokeIndex));
        Assert.Contains("catch (...)", invokeBody);

        // Every COM surface that allocates must be protected too. Count rather than substring
        // search: the first occurrence of a name can be a declaration, not the body.
        foreach (string surface in new[] { "GetTitle", "GetIcon", "GetToolTip", "GetState", "Invoke", "DllGetClassObject" })
        {
            Assert.Contains(surface, source);
        }

        int catches = CountOccurrences(source, "catch (...)");
        Assert.True(catches >= 6, $"Expected a catch-all on every allocating COM surface, found {catches}.");

        // Catches only work on destructible frames when the compiler models exceptions.
        string project = File.ReadAllText(Path.Combine(root, "src", "Ornare.ShellExtension", "Ornare.ShellExtension.vcxproj"));
        Assert.Contains("<ExceptionHandling>Async</ExceptionHandling>", project);
    }

    [Fact]
    public void InputImageSizeLimitIsSharedNotDuplicated()
    {
        string root = FindRepositoryRoot();
        string constants = File.ReadAllText(Path.Combine(root, "src", "Ornare.Core", "OrnareConstants.cs"));
        Assert.Contains("public const long MaxInputImageBytes", constants);

        // The panel must reference the shared constant, and the size check must come before the
        // read: a bound applied after ReadAllBytesAsync is already too late for the allocation.
        string code = ReadAllQuickPanelPartials();
        int guardIndex = code.IndexOf("fileLength > OrnareConstants.MaxInputImageBytes", StringComparison.Ordinal);
        int readIndex = code.IndexOf("File.ReadAllBytesAsync(imagePath)", StringComparison.Ordinal);
        Assert.True(guardIndex >= 0, "StageImageAsync does not refuse an oversized file.");
        Assert.True(readIndex >= 0, "StageImageAsync no longer reads the picked file.");
        Assert.True(guardIndex < readIndex, "The size check must precede File.ReadAllBytesAsync.");

        // The clipboard path never goes through StageImageAsync, so it needs its own bound.
        Assert.Contains("stream.Size > (ulong)OrnareConstants.MaxInputImageBytes", code);
    }

    /// <summary>
    /// Audit finding 1.1. On an untouched folder the panel seeded the picker with #2563EB, the
    /// ColorChanged handler treated that seed as a user choice, the folder became
    /// "Unapplied changes" and Apply was enabled; clicking it wrote a colour nobody picked.
    /// Programmatic writes must go through <c>SetPickerColorSilently</c>.
    /// </summary>
    [Fact]
    public void ProgrammaticColourWritesNeverStageAColour()
    {
        string code = ReadAllQuickPanelPartials();

        Assert.Contains("private void SetPickerColorSilently(", code);
        Assert.Contains("private bool _suppressColorPickerEvents", code);

        // A direct assignment is only legitimate inside the helper, and on the deliberate
        // recent-colour chip click, which is a real choice and must preview.
        const string assignment = "CustomColorPicker.Color =";
        var assignmentPositions = new List<int>();
        int scan = 0;
        while ((scan = code.IndexOf(assignment, scan, StringComparison.Ordinal)) >= 0)
        {
            int valueIndex = scan + assignment.Length;
            bool isComparison = valueIndex < code.Length && code[valueIndex] == '=';
            if (!isComparison)
            {
                assignmentPositions.Add(scan);
            }

            scan = valueIndex;
        }

        int helperStart = code.IndexOf("private void SetPickerColorSilently(", StringComparison.Ordinal);
        int helperEnd = code.IndexOf("private void TriggerCustomColorPreview", helperStart, StringComparison.Ordinal);
        int chipStart = code.IndexOf("chip.Click += (s, _) =>", StringComparison.Ordinal);
        Assert.True(chipStart >= 0, "The recent-colour chip click handler is missing.");

        Assert.Equal(2, assignmentPositions.Count);
        Assert.True(
            assignmentPositions[0] > helperStart && assignmentPositions[0] < helperEnd,
            "The seed assignment must live inside SetPickerColorSilently.");
        Assert.True(
            assignmentPositions[1] > chipStart && assignmentPositions[1] < chipStart + 400,
            "The only other direct assignment must be the recent-colour chip click.");

        // The helper must raise and clear the suppression flag around the assignment, so an
        // exception cannot leave the picker permanently deaf.
        int suppress = code.IndexOf("_suppressColorPickerEvents = true;", StringComparison.Ordinal);
        int restore = code.IndexOf("_suppressColorPickerEvents = previous;", StringComparison.Ordinal);
        Assert.True(suppress >= 0 && restore > suppress, "SetPickerColorSilently does not bracket the assignment with the guard.");

        // The handler must reject the seeded event before staging anything.
        int handlerStart = code.IndexOf("private void OnCustomColorPickerColorChanged", StringComparison.Ordinal);
        Assert.True(handlerStart >= 0, "OnCustomColorPickerColorChanged is missing.");
        int handlerEnd = code.IndexOf("private void SetPickerColorSilently", handlerStart, StringComparison.Ordinal);
        string handler = code[handlerStart..handlerEnd];
        Assert.Contains("if (_suppressColorPickerEvents)", handler);
        Assert.True(
            handler.IndexOf("if (_suppressColorPickerEvents)", StringComparison.Ordinal)
                < handler.IndexOf("TriggerCustomColorPreview()", StringComparison.Ordinal),
            "The suppression check must run before TriggerCustomColorPreview().");

        // Switching target folders must cancel a preview debounce scheduled for the old folder.
        int applyTarget = code.IndexOf("private async Task ApplyTargetFolderAsync", StringComparison.Ordinal);
        Assert.True(applyTarget >= 0, "ApplyTargetFolderAsync is missing.");
        int nextMember = code.IndexOf("private async Task", applyTarget + 10, StringComparison.Ordinal);
        string applyTargetBody = nextMember > applyTarget ? code[applyTarget..nextMember] : code[applyTarget..];
        Assert.Contains("_previewDebounceCts?.Cancel();", applyTargetBody);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
