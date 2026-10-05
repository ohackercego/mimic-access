using System.Security.Cryptography;

namespace MimicPartyAccess;

/// <summary>
/// Logs hashes for the game binaries so compatibility can be checked against the exact build.
/// </summary>
internal static class BuildFingerprint
{
    public static void Log(IModLogger logger)
    {
        LogFileHash(logger, "GameAssembly.dll");
        LogFileHash(logger, Path.Combine("Mimic Party_Data", "il2cpp_data", "Metadata", "global-metadata.dat"));
    }

    private static void LogFileHash(IModLogger logger, string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            logger.Warning($"Could not fingerprint '{relativePath}': file not found.");
            return;
        }

        using var stream = File.OpenRead(fullPath);
        using var sha256 = SHA256.Create();
        var hash = Convert.ToHexString(sha256.ComputeHash(stream));
        logger.Info($"Build fingerprint {relativePath}: {hash}");
    }
}
