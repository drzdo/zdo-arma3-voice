namespace ZdoArmaVoice.Server.Game;

/// <summary>
/// Loads SQF data files from data/ directory and sends them to the game on connect.
/// Replaces the old CommandRegistry + function registration system.
/// Files are enumerated recursively and sent in alphabetical order.
/// </summary>
public class DataLoader
{
    private readonly List<string> _sqfFiles = [];

    /// <summary>
    /// Load all .sqf files from the data directory, sorted alphabetically by relative path.
    /// </summary>
    public void LoadData(string dataDir)
    {
        if (!Directory.Exists(dataDir))
        {
            Log.Warn("DataLoader", $"Data directory not found: {dataDir}");
            return;
        }

        _sqfFiles.Clear();

        var files = Directory.GetFiles(dataDir, "*.sqf", SearchOption.AllDirectories)
            .Select(f => (FullPath: f, RelPath: Path.GetRelativePath(dataDir, f)))
            .OrderBy(f => f.RelPath, StringComparer.Ordinal)
            .ToList();

        foreach (var (fullPath, relPath) in files)
        {
            _sqfFiles.Add(File.ReadAllText(fullPath).Trim());
            Log.Info("DataLoader", $"Loaded: {relPath}");
        }

        Log.Info("DataLoader", $"{_sqfFiles.Count} SQF files loaded.");
    }

    /// <summary>
    /// Send all loaded SQF files to the game as fire-and-forget RPCs.
    /// Called on client connect.
    /// </summary>
    public void RegisterAll(RpcClient rpc)
    {
        Log.Info("DataLoader", $"Sending {_sqfFiles.Count} SQF files to game...");
        foreach (var sqf in _sqfFiles)
        {
            rpc.Fire(sqf);
        }
        Log.Info("DataLoader", "Done.");
    }
}
