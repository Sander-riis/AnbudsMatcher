using System.Text.Json;

namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-05: When the notices cache contains an old version string, CheckVersionMismatch returns true
/// and PurgeAllCaches deletes all three cache files atomically.
/// </summary>
public class CacheVersionTests
{
    [Fact]
    public void VersionMismatch_DeletesAllThreeCacheFiles()
    {
        // IT-05: Arrange — plant three fake cache files in a temp directory.
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        var noticesFile = Path.Combine(tempDir, "notices-cache.json");
        var reportsFile = Path.Combine(tempDir, "reports-cache.json");
        var matchesFile = Path.Combine(tempDir, "matches-cache.json");

        try
        {
            // Plant notices-cache.json with a stale version string
            var staleEnvelope = new { Version = "old-version-string", Data = new[] { new { Id = 1 } } };
            File.WriteAllText(noticesFile, JsonSerializer.Serialize(staleEnvelope));
            File.WriteAllText(reportsFile, "{}");
            File.WriteAllText(matchesFile, "{}");

            // Act — check version mismatch (expects "it-v1|IKT,IT,..." but finds "old-version-string")
            var currentVersion = "it-v1|IKT,IT,utvikling,programvare,skytjeneste,digitalisering|COMPETITION,PLANNING";
            bool isMismatch = DoffinService.CheckVersionMismatch(noticesFile, currentVersion);

            Assert.True(isMismatch, "CheckVersionMismatch should return true for stale version");

            // Act — purge caches
            DoffinService.PurgeAllCaches(noticesFile, reportsFile, matchesFile);

            // Assert — all three files deleted
            Assert.False(File.Exists(noticesFile), "notices-cache.json should be deleted");
            Assert.False(File.Exists(reportsFile), "reports-cache.json should be deleted");
            Assert.False(File.Exists(matchesFile), "matches-cache.json should be deleted");
        }
        finally
        {
            // Cleanup temp directory (best-effort)
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
