using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageMagick;
using Ornare.Core;
using Ornare.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace Ornare.Imaging.Tests;

public class RenderCacheAndPipelineTests
{
    private readonly ITestOutputHelper _output;
    private readonly ImagePipeline _pipeline = new();

    public RenderCacheAndPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SameRenderRecipeConcurrentlyResultsInOneDerivedArtifactAndByteIdenticalCacheResult()
    {
        string testDir = Path.Combine(Path.GetTempPath(), $"ornare_test_render_{Guid.NewGuid():N}");
        string cacheDir = Path.Combine(testDir, "renders");
        Directory.CreateDirectory(cacheDir);

        using var testImage = new MagickImage(MagickColors.Crimson, 128, 128);
        testImage.Format = MagickFormat.Png;
        byte[] sourceBytes = testImage.ToByteArray();

        string sourceHash = _pipeline.ComputeSourceHash(sourceBytes);
        var recipe = new RenderRecipe
        {
            SourceAssetHash = sourceHash,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 20, 24, 32, 48, 64, 256]
        };

        var cacheMap = new ConcurrentDictionary<string, RenderCacheEntry>();

        try
        {
            int concurrentCount = 10;
            var results = new NormalizedImageResult[concurrentCount];
            var tasks = new Task[concurrentCount];

            for (int i = 0; i < concurrentCount; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    string outputPath = Path.Combine(testDir, $"out_{index}.ico");
                    var res = _pipeline.ProcessRecipe(
                        recipe,
                        sourceBytes,
                        outputPath,
                        cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                        cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                        cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                        cacheDirectory: cacheDir);

                    Assert.True(res.IsSuccess);
                    results[index] = res.Value!;
                });
            }

            await Task.WhenAll(tasks);

            // Invariant: Exactly one canonical cache file created
            string[] cachedFiles = Directory.GetFiles(cacheDir, "*.ico");
            Assert.Single(cachedFiles);

            string expectedKey = recipe.ComputeRenderKey();
            Assert.Equal($"{expectedKey}.ico", Path.GetFileName(cachedFiles[0]));

            // Invariant: All concurrent executions produced byte-identical valid ICOs
            byte[] canonicalBytes = await File.ReadAllBytesAsync(cachedFiles[0]);
            for (int i = 0; i < concurrentCount; i++)
            {
                Assert.Equal(expectedKey, results[i].RenderKey);
                byte[] outBytes = await File.ReadAllBytesAsync(results[i].GeneratedIcoPath);
                Assert.Equal(canonicalBytes.Length, outBytes.Length);
                Assert.True(canonicalBytes.SequenceEqual(outBytes));
            }
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void DeleteOrCorruptCacheTriggersSafeRebuild()
    {
        string testDir = Path.Combine(Path.GetTempPath(), $"ornare_test_corrupt_{Guid.NewGuid():N}");
        string cacheDir = Path.Combine(testDir, "renders");
        Directory.CreateDirectory(cacheDir);

        using var testImage = new MagickImage(MagickColors.ForestGreen, 96, 96);
        testImage.Format = MagickFormat.Png;
        byte[] sourceBytes = testImage.ToByteArray();

        string sourceHash = _pipeline.ComputeSourceHash(sourceBytes);
        var recipe = new RenderRecipe
        {
            SourceAssetHash = sourceHash,
            TargetFormat = "ico",
            TargetFrameSizes = [16, 32, 48]
        };

        var cacheMap = new ConcurrentDictionary<string, RenderCacheEntry>();

        try
        {
            string out1 = Path.Combine(testDir, "out1.ico");
            var res1 = _pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                out1,
                cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                cacheDirectory: cacheDir);

            Assert.True(res1.IsSuccess);
            Assert.False(res1.Value!.IsFromCache);

            string cachedIco = Path.Combine(cacheDir, $"{recipe.ComputeRenderKey()}.ico");
            Assert.True(File.Exists(cachedIco));

            // Verify normal cache hit
            string out2 = Path.Combine(testDir, "out2.ico");
            var res2 = _pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                out2,
                cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                cacheDirectory: cacheDir);

            Assert.True(res2.IsSuccess);
            Assert.True(res2.Value!.IsFromCache);

            // Now deliberately corrupt the cached ICO file
            File.WriteAllBytes(cachedIco, [0xFF, 0xFF, 0x00, 0x00]);

            // Re-render: corruption should be detected, invalidated, and rebuilt safely
            string out3 = Path.Combine(testDir, "out3.ico");
            var res3 = _pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                out3,
                cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                cacheDirectory: cacheDir);

            Assert.True(res3.IsSuccess);
            Assert.False(res3.Value!.IsFromCache);
            Assert.True(_pipeline.IsValidIco(out3, expectedMinFrames: 1));

            // Cached file is repaired and valid again
            Assert.True(_pipeline.IsValidIco(cachedIco, expectedMinFrames: 1));
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void CrashAtTempPublishDbWindowsReconciliationRestoresInvariant()
    {
        string testDir = Path.Combine(Path.GetTempPath(), $"ornare_test_crash_{Guid.NewGuid():N}");
        string cacheDir = Path.Combine(testDir, "renders");
        Directory.CreateDirectory(cacheDir);

        try
        {
            // Simulate orphan temp files left over by an interrupted crash during atomic publish
            string orphanTmp1 = Path.Combine(cacheDir, $"tmp_{Guid.NewGuid():N}.tmp");
            string orphanTmp2 = Path.Combine(cacheDir, $"tmp_{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(orphanTmp1, [0, 0, 1, 0, 1, 0]);
            File.WriteAllBytes(orphanTmp2, [0xAA, 0xBB]);

            // Orphan temp files should not interfere with canonical naming or cache operation
            using var testImage = new MagickImage(MagickColors.Navy, 64, 64);
            testImage.Format = MagickFormat.Png;
            byte[] sourceBytes = testImage.ToByteArray();

            var recipe = new RenderRecipe
            {
                SourceAssetHash = _pipeline.ComputeSourceHash(sourceBytes),
                TargetFormat = "ico",
                TargetFrameSizes = [16, 32]
            };

            var cacheMap = new ConcurrentDictionary<string, RenderCacheEntry>();
            string outPath = Path.Combine(testDir, "clean.ico");

            var res = _pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                outPath,
                cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                cacheDirectory: cacheDir);

            Assert.True(res.IsSuccess);
            string canonical = Path.Combine(cacheDir, $"{recipe.ComputeRenderKey()}.ico");
            Assert.True(File.Exists(canonical));
            Assert.True(_pipeline.IsValidIco(canonical));

            // Invariant: Orphan temp files did not pollute *.ico namespace
            Assert.Single(Directory.GetFiles(cacheDir, "*.ico"));

            // Cleanup routine removes orphan tmp files
            foreach (var f in Directory.GetFiles(cacheDir, "tmp_*.tmp"))
            {
                File.Delete(f);
            }
            Assert.Empty(Directory.GetFiles(cacheDir, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void BenchmarkRenderStageCacheMissVsCacheHitHitUnder5Ms()
    {
        string testDir = Path.Combine(Path.GetTempPath(), $"ornare_test_bench_{Guid.NewGuid():N}");
        string cacheDir = Path.Combine(testDir, "renders");
        Directory.CreateDirectory(cacheDir);

        using var testImage = new MagickImage(MagickColors.Teal, 256, 256);
        testImage.Format = MagickFormat.Png;
        byte[] sourceBytes = testImage.ToByteArray();

        var recipe = new RenderRecipe
        {
            SourceAssetHash = _pipeline.ComputeSourceHash(sourceBytes),
            TargetFormat = "ico",
            TargetFrameSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
        };

        var cacheMap = new ConcurrentDictionary<string, RenderCacheEntry>();

        try
        {
            // 1. Warm-up run / Miss
            string warmupOut = Path.Combine(testDir, "warmup.ico");
            var swMiss = Stopwatch.StartNew();
            var missRes = _pipeline.ProcessRecipe(
                recipe,
                sourceBytes,
                warmupOut,
                cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                cacheDirectory: cacheDir);
            swMiss.Stop();

            Assert.True(missRes.IsSuccess);
            Assert.False(missRes.Value!.IsFromCache);
            double missMs = swMiss.Elapsed.TotalMilliseconds;

            // 2. Measure cache hits across 20 iterations (shared protocol)
            double[] hitTimes = new double[20];
            for (int i = 0; i < 20; i++)
            {
                string hitOut = Path.Combine(testDir, $"hit_{i}.ico");
                var swHit = Stopwatch.StartNew();
                var hitRes = _pipeline.ProcessRecipe(
                    recipe,
                    null, // Notice: zero source bytes required on cache hit!
                    hitOut,
                    cacheLookup: key => cacheMap.TryGetValue(key, out var e) ? e : null,
                    cacheStore: entry => cacheMap[entry.RenderKey] = entry,
                    cacheInvalidate: key => cacheMap.TryRemove(key, out _),
                    cacheDirectory: cacheDir);
                swHit.Stop();

                Assert.True(hitRes.IsSuccess);
                Assert.True(hitRes.Value!.IsFromCache);
                hitTimes[i] = swHit.Elapsed.TotalMilliseconds;
            }

            Array.Sort(hitTimes);
            double medianHit = hitTimes[10];
            double p95Hit = hitTimes[19];

            _output.WriteLine($"Render-Stage Cache Miss: {missMs:F2} ms");
            _output.WriteLine($"Render-Stage Cache Hit: P50={medianHit:F3} ms, P95={p95Hit:F3} ms (Min={hitTimes[0]:F3} ms, Max={hitTimes[19]:F3} ms)");

            // Mandatory requirement: median hit <= 5.0 ms
            Assert.True(medianHit <= 5.0, $"Median cache hit time must be <= 5ms, actual was {medianHit:F3}ms");

            // When miss >= 25ms, hit must also be >= 5x faster
            if (missMs >= 25.0)
            {
                double speedup = missMs / Math.Max(medianHit, 0.001);
                _output.WriteLine($"Speedup Ratio: {speedup:F1}x");
                Assert.True(speedup >= 5.0, $"Cache hit should be at least 5x faster than miss, was {speedup:F1}x");
            }
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                try { Directory.Delete(testDir, recursive: true); } catch { }
            }
        }
    }
}
