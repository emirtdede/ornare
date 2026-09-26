namespace Ornare.IntegrationTests;

using System;
using System.IO;
using Ornare.Core;
using Ornare.Windows;
using Xunit;

public sealed class PortableFolderReconciliationTests
{
    private static FolderAssignment CreateTestAssignment(
        string folderPath,
        uint? volumeSerialNumber = null,
        ulong? fileId64 = null)
    {
        return new FolderAssignment
        {
            Id = Guid.NewGuid().ToString(),
            FolderPath = folderPath,
            OwnershipEpochId = Guid.NewGuid(),
            IconRelativePath = @".ornare\folder.ico,0",
            Status = FolderCustomizationState.Healthy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            VolumeSerialNumber = volumeSerialNumber,
            FileId64 = fileId64
        };
    }

    [Fact]
    public void IsSupportedFixedNtfsVolumeValidatesDriveCorrectly()
    {
        string currentRoot = Path.GetPathRoot(Environment.CurrentDirectory) ?? @"C:\";
        bool isCurrentNtfs = FolderIdentityService.IsSupportedFixedNtfsVolume(currentRoot);

        // Windows CI and dev workstations run on NTFS
        Assert.True(isCurrentNtfs, $"Expected root '{currentRoot}' to be a supported fixed NTFS volume.");

        // Invalid paths must return false
        Assert.False(FolderIdentityService.IsSupportedFixedNtfsVolume(string.Empty));
        Assert.False(FolderIdentityService.IsSupportedFixedNtfsVolume(@"\\server\share\folder"));
        Assert.False(FolderIdentityService.IsSupportedFixedNtfsVolume(@"Z:\NonExistentPath"));
    }

    [Fact]
    public void GetNtfsIdentityOnLiveFolderReturnsStableNonZeroIdentity()
    {
        string testDir = Path.Combine(Path.GetTempPath(), $"ornare_id_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);

        try
        {
            var identity = FolderIdentityService.GetNtfsIdentity(testDir);
            Assert.True(identity.HasValue, "Expected valid NTFS identity on local filesystem.");
            Assert.NotEqual(0u, identity.Value.VolumeSerialNumber);
            Assert.NotEqual(0ul, identity.Value.FileId64);

            Assert.Equal(8, identity.Value.VolumeSerialHex.Length);
            Assert.Equal(16, identity.Value.FileIdHex.Length);

            var (vHex, fHex) = FolderIdentityService.GetFolderIdentity(testDir);
            Assert.Equal(identity.Value.VolumeSerialHex, vHex);
            Assert.Equal(identity.Value.FileIdHex, fHex);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void RenameAndRelocationTryReopenAndReconcilePathRecoversCanonicalPath()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), $"ornare_reloc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        string originalPath = Path.Combine(rootDir, "OriginalProjectFolder");
        Directory.CreateDirectory(originalPath);

        string relocatedPath = Path.Combine(rootDir, "RenamedAndRelocatedProjectFolder");

        try
        {
            var identity = FolderIdentityService.GetNtfsIdentity(originalPath);
            Assert.True(identity.HasValue, "Failed to get NTFS identity for original path.");

            // Rename the folder on NTFS
            Directory.Move(originalPath, relocatedPath);

            Assert.False(Directory.Exists(originalPath));
            Assert.True(Directory.Exists(relocatedPath));

            // Attempt to resolve by NTFS file index
            bool recovered = FolderIdentityService.TryReopenAndReconcilePath(
                identity.Value.VolumeSerialNumber,
                identity.Value.FileId64,
                originalPath,
                out string? canonicalPath);

            Assert.True(recovered, "TryReopenAndReconcilePath should recover folder path by file ID.");
            Assert.NotNull(canonicalPath);
            Assert.Equal(Path.GetFullPath(relocatedPath), Path.GetFullPath(canonicalPath));
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                try { Directory.Delete(rootDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void EvaluateAssignmentWhenFolderRelocatedReturnsRelocatedDecision()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), $"ornare_eval_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        string originalPath = Path.Combine(rootDir, "OriginalFolder");
        Directory.CreateDirectory(originalPath);

        string newPath = Path.Combine(rootDir, "MovedFolder");

        try
        {
            var identity = FolderIdentityService.GetNtfsIdentity(originalPath);
            Assert.True(identity.HasValue);

            var assignment = CreateTestAssignment(
                originalPath,
                identity.Value.VolumeSerialNumber,
                identity.Value.FileId64);

            // Move the folder
            Directory.Move(originalPath, newPath);

            // Evaluate assignment
            var decision = FolderReconciliationService.EvaluateAssignment(assignment);

            Assert.Equal(ReconciliationOutcome.Relocated, decision.Outcome);
            Assert.NotNull(decision.RelocatedPath);
            Assert.Equal(Path.GetFullPath(newPath), Path.GetFullPath(decision.RelocatedPath));
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                try { Directory.Delete(rootDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void EvaluateAssignmentWhenPathReplacedByNewFolderDetectsIdentityMismatchAndRelocates()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), $"ornare_mismatch_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        string path = Path.Combine(rootDir, "TrackedFolder");
        Directory.CreateDirectory(path);

        string movedPath = Path.Combine(rootDir, "MovedAwayFolder");

        try
        {
            var origIdentity = FolderIdentityService.GetNtfsIdentity(path);
            Assert.True(origIdentity.HasValue);

            var assignment = CreateTestAssignment(
                path,
                origIdentity.Value.VolumeSerialNumber,
                origIdentity.Value.FileId64);

            // Move the original folder to movedPath
            Directory.Move(path, movedPath);

            // Create a brand new folder at the original path
            Directory.CreateDirectory(path);

            var newIdentity = FolderIdentityService.GetNtfsIdentity(path);
            Assert.True(newIdentity.HasValue);

            // Identities must differ
            Assert.NotEqual(origIdentity.Value.FileId64, newIdentity.Value.FileId64);

            // Evaluation must NOT falsely claim the new folder at 'path' is healthy for the assignment.
            // It must detect the identity mismatch, locate the original folder at movedPath, and return Relocated!
            var decision = FolderReconciliationService.EvaluateAssignment(assignment);

            Assert.Equal(ReconciliationOutcome.Relocated, decision.Outcome);
            Assert.NotNull(decision.RelocatedPath);
            Assert.Equal(Path.GetFullPath(movedPath), Path.GetFullPath(decision.RelocatedPath));
        }
        finally
        {
            if (Directory.Exists(rootDir))
            {
                try { Directory.Delete(rootDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void EvaluateAssignmentWhenFolderDeletedCompletelyReturnsLocationUnknownWithoutDiskScan()
    {
        string rootDir = Path.Combine(Path.GetTempPath(), $"ornare_del_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        string folderPath = Path.Combine(rootDir, "DeletedFolder");
        Directory.CreateDirectory(folderPath);

        var identity = FolderIdentityService.GetNtfsIdentity(folderPath);
        Assert.True(identity.HasValue);

        var assignment = CreateTestAssignment(
            folderPath,
            identity.Value.VolumeSerialNumber,
            identity.Value.FileId64);

        // Delete folder completely
        Directory.Delete(folderPath);
        Assert.False(Directory.Exists(folderPath));

        var decision = FolderReconciliationService.EvaluateAssignment(assignment);

        Assert.Equal(ReconciliationOutcome.LocationUnknown, decision.Outcome);
        Assert.Equal(FolderCustomizationState.LocationUnknown, decision.RecommendedState);
        Assert.NotNull(decision.Reason);

        if (Directory.Exists(rootDir))
        {
            try { Directory.Delete(rootDir, true); } catch { }
        }
    }

    [Fact]
    public void EvaluateAssignmentWhenAssignmentHasNoNtfsIdentityReturnsMissingTarget()
    {
        string missingPath = @"C:\NonExistentTestFolder_" + Guid.NewGuid().ToString("N");

        var assignment = CreateTestAssignment(missingPath);

        var decision = FolderReconciliationService.EvaluateAssignment(assignment);

        Assert.Equal(ReconciliationOutcome.MissingTarget, decision.Outcome);
        Assert.Equal(FolderCustomizationState.MissingTarget, decision.RecommendedState);
    }
}
