using System;
using System.Collections.Generic;
using System.IO;
using Ornare.Core;
using Ornare.Imaging;
using Ornare.Windows;
using Xunit;

namespace Ornare.IntegrationTests;

/// <summary>
/// Locks down the journal transition contract of <see cref="FolderCustomizer"/>.
/// <para>
/// The filesystem layer must never publish <see cref="JournalState.Completed"/>: the
/// calling service owns the post-commit metadata writes. Publishing Completed early let a
/// crash leave a committed icon on disk while startup reconciliation marked the operation
/// rolled back (INV-FS-006, INV-TX-002).
/// </para>
/// </summary>
public class JournalStageContractTests
{
    [Fact]
    public void ApplyIconEmitsMetadataCommittedButNeverCompleted()
    {
        using var scope = new JournalScope();
        ImagePipeline pipeline = new();
        string stagedIco = Path.Combine(scope.Path, "staged.ico");

        var render = FolderTemplateRenderer.RenderColoredTemplateIco(pipeline, "#0078D4", stagedIco);
        Assert.True(render.IsSuccess);

        var stages = new List<JournalState>();
        var result = new FolderCustomizer().ApplyIcon(scope.Folder, stagedIco, stages.Add);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Contains(JournalState.FilesystemCommitted, stages);
        Assert.Contains(JournalState.ShellRefreshRequested, stages);
        Assert.Contains(JournalState.MetadataCommitted, stages);
        Assert.DoesNotContain(JournalState.Completed, stages);
    }

    [Fact]
    public void ResetIconNeverPublishesCompleted()
    {
        using var scope = new JournalScope();
        ImagePipeline pipeline = new();
        string stagedIco = Path.Combine(scope.Path, "staged.ico");

        var render = FolderTemplateRenderer.RenderColoredTemplateIco(pipeline, "#0078D4", stagedIco);
        Assert.True(render.IsSuccess);

        FolderCustomizer customizer = new();
        FolderIconSemantics prior = DesktopIniParser.ReadSemantics(scope.Folder);
        FolderBaseline baseline = new()
        {
            FolderAssignmentId = Guid.NewGuid(),
            OwnershipEpochId = Guid.NewGuid(),
            PriorSemantics = prior,
            CapturedAt = DateTimeOffset.UtcNow
        };

        var apply = customizer.ApplyIcon(scope.Folder, stagedIco);
        Assert.True(apply.IsSuccess, apply.ErrorMessage);

        var stages = new List<JournalState>();
        var reset = customizer.ResetIcon(scope.Folder, baseline, stages.Add);

        Assert.True(reset.IsSuccess, reset.ErrorMessage);
        Assert.DoesNotContain(JournalState.Completed, stages);
    }

    private sealed class JournalScope : IDisposable
    {
        public JournalScope()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ornare_journal_" + Guid.NewGuid().ToString("N"));
            Folder = System.IO.Path.Combine(Path, "Target");
            Directory.CreateDirectory(Folder);
        }

        public string Path { get; }

        public string Folder { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Temp cleanup must never fail a test.
            }
        }
    }
}
