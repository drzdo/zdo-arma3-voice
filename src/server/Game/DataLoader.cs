using System.Text;
using System.Text.RegularExpressions;

namespace ZdoArmaVoice.Server.Game;

/// <summary>
/// Loads SQF data files from data/ directory and sends them to the game on connect.
/// Replaces the old CommandRegistry + function registration system.
/// Files are enumerated recursively and sent in alphabetical order.
/// Comments are stripped before transmission to stay within callExtension's ~10KB buffer.
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
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .Select(f => (FullPath: f, RelPath: Path.GetRelativePath(dataDir, f)))
            .OrderBy(f => f.RelPath, StringComparer.Ordinal)
            .ToList();

        foreach (var (fullPath, relPath) in files)
        {
            var raw = File.ReadAllText(fullPath).Trim();
            var stripped = StripSqfComments(raw);
            _sqfFiles.Add(stripped);
            Log.Info("DataLoader", $"Loaded: {relPath} ({raw.Length} -> {stripped.Length} bytes)");
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

    /// <summary>
    /// Strip // and /* */ comments from SQF, respecting string literals.
    /// Collapses whitespace to reduce size for callExtension buffer.
    /// </summary>
    private static string StripSqfComments(string sqf)
    {
        var sb = new StringBuilder(sqf.Length);
        int i = 0;
        bool inString = false;

        while (i < sqf.Length)
        {
            if (inString)
            {
                sb.Append(sqf[i]);
                if (sqf[i] == '"')
                {
                    // SQF escapes quotes by doubling: ""
                    if (i + 1 < sqf.Length && sqf[i + 1] == '"')
                    {
                        sb.Append('"');
                        i += 2;
                        continue;
                    }
                    inString = false;
                }
                i++;
            }
            else if (sqf[i] == '"')
            {
                inString = true;
                sb.Append(sqf[i]);
                i++;
            }
            else if (i + 1 < sqf.Length && sqf[i] == '/' && sqf[i + 1] == '/')
            {
                // Single-line comment — skip to end of line
                while (i < sqf.Length && sqf[i] != '\n') i++;
            }
            else if (i + 1 < sqf.Length && sqf[i] == '/' && sqf[i + 1] == '*')
            {
                // Multi-line comment — skip to */
                i += 2;
                while (i + 1 < sqf.Length && !(sqf[i] == '*' && sqf[i + 1] == '/')) i++;
                if (i + 1 < sqf.Length) i += 2;
            }
            else
            {
                sb.Append(sqf[i]);
                i++;
            }
        }

        // Collapse runs of whitespace into single spaces
        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
}
