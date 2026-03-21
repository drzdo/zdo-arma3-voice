using System.Text.Json;

namespace ArmaVoice.Server.Game;

/// <summary>
/// Holds the latest game state snapshot, updated from JSON state messages.
/// </summary>
public class GameState
{
    public float[] PlayerPos { get; set; } = [0, 0, 0];
    public float PlayerDir { get; set; }
    public List<(string NetId, float[] Pos)> NearbyUnits { get; } = new();

    /// <summary>
    /// Update from JSON state message: {"t":"state","p":[x,y,z],"d":dir,"u":[["netId",[x,y,z]],...]}
    /// </summary>
    public void UpdateFromState(JsonElement root)
    {
        try
        {
            // Player position
            if (root.TryGetProperty("p", out var p) && p.ValueKind == JsonValueKind.Array && p.GetArrayLength() >= 3)
            {
                PlayerPos = [p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()];
            }

            // Player direction
            if (root.TryGetProperty("d", out var d))
            {
                PlayerDir = d.GetSingle();
            }

            // Nearby units
            NearbyUnits.Clear();
            if (root.TryGetProperty("u", out var u) && u.ValueKind == JsonValueKind.Array)
            {
                foreach (var unit in u.EnumerateArray())
                {
                    if (unit.ValueKind == JsonValueKind.Array && unit.GetArrayLength() >= 2)
                    {
                        var netId = unit[0].GetString() ?? "";
                        var posEl = unit[1];
                        float[] pos = [0, 0, 0];
                        if (posEl.ValueKind == JsonValueKind.Array && posEl.GetArrayLength() >= 3)
                        {
                            pos = [posEl[0].GetSingle(), posEl[1].GetSingle(), posEl[2].GetSingle()];
                        }
                        NearbyUnits.Add((netId, pos));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("GameState", $"Error parsing state: {ex.Message}");
        }
    }
}
