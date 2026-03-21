using System.Globalization;
using ArmaVoice.Server.Game;

namespace ArmaVoice.Server.Ai;

/// <summary>
/// Executes parsed intents by resolving units/locations and firing SQF commands.
/// No text parsing — all parsing is done by the LLM in IntentParser.
/// </summary>
public class CommandExecutor
{
    private readonly RpcClient _rpc;
    private readonly UnitRegistry _unitRegistry;
    private readonly GameState _gameState;
    private readonly DialogueManager _dialogueManager;

    public CommandExecutor(RpcClient rpc, UnitRegistry unitRegistry, GameState gameState, DialogueManager dialogueManager)
    {
        _rpc = rpc;
        _unitRegistry = unitRegistry;
        _gameState = gameState;
        _dialogueManager = dialogueManager;
    }

    public async Task ExecuteAsync(IntentParsed intent, float[] lookTarget)
    {
        var netIds = await ResolveUnitsAsync(intent.Units);

        switch (intent.Action.ToLowerInvariant())
        {
            case "move":
                if (netIds.Count == 0) { Log("Move", "no units resolved."); break; }
                var pos = ResolveLocation(intent.Location, lookTarget);
                _rpc.Fire($"[[{FmtIds(netIds)}], {FmtPos(pos)}] call arma3_mic_fnc_moveUnits");
                Log("Move", $"{string.Join(",", netIds)} → {FmtPos(pos)}");
                break;

            case "attack":
                if (netIds.Count == 0) { Log("Attack", "no units resolved."); break; }
                var targetNetId = ResolveUnitRef(intent.Target);
                if (targetNetId == null) { Log("Attack", $"target \"{intent.Target}\" not found."); break; }
                _rpc.Fire($"[[{FmtIds(netIds)}], '{targetNetId}'] call arma3_mic_fnc_attackTarget");
                Log("Attack", $"{string.Join(",", netIds)} → {targetNetId}");
                break;

            case "stop":
                if (netIds.Count == 0) { Log("Stop", "no units resolved."); break; }
                _rpc.Fire($"[[{FmtIds(netIds)}]] call arma3_mic_fnc_stop");
                Log("Stop", string.Join(",", netIds));
                break;

            case "hold":
                if (netIds.Count == 0) { Log("Hold", "no units resolved."); break; }
                _rpc.Fire($"[[{FmtIds(netIds)}]] call arma3_mic_fnc_holdPosition");
                Log("Hold", string.Join(",", netIds));
                break;

            case "drop":
                if (netIds.Count == 0) { Log("Drop", "no units resolved."); break; }
                _rpc.Fire($"[[{FmtIds(netIds)}], 'DOWN'] call arma3_mic_fnc_setStance");
                Log("Drop", string.Join(",", netIds));
                break;

            case "regroup":
                if (netIds.Count == 0) { Log("Regroup", "no units resolved."); break; }
                _rpc.Fire($"[[{FmtIds(netIds)}]] call arma3_mic_fnc_regroup");
                Log("Regroup", string.Join(",", netIds));
                break;

            case "formation":
                if (string.IsNullOrEmpty(intent.Formation)) { Log("Formation", "missing."); break; }
                _rpc.Fire($"['{intent.Formation}'] call arma3_mic_fnc_setFormation");
                Log("Formation", intent.Formation);
                break;

            case "dialogue":
                var npcNetId = ResolveUnitRef(intent.Target);
                if (npcNetId == null) { Log("Dialogue", $"target \"{intent.Target}\" not found."); break; }
                _dialogueManager.Enqueue(npcNetId, intent.Text ?? "");
                Log("Dialogue", $"queued → {npcNetId}");
                break;

            default:
                Log("Unknown", intent.Action);
                break;
        }

        // Apply optional modifiers
        if (!string.IsNullOrEmpty(intent.Stance) && netIds.Count > 0)
        {
            _rpc.Fire($"[[{FmtIds(netIds)}], '{intent.Stance}'] call arma3_mic_fnc_setStance");
            Log("Stance", intent.Stance);
        }

        if (!string.IsNullOrEmpty(intent.Speed))
        {
            _rpc.Fire($"['{intent.Speed}'] call arma3_mic_fnc_setSpeed");
            Log("Speed", intent.Speed);
        }
    }

    // ── Unit resolution ──────────────────────────────────────
    // LLM returns netIds, team colors, "all", or names (fallback).
    // C# resolves to actual netIds — no text parsing.

    private async Task<List<string>> ResolveUnitsAsync(List<string> refs)
    {
        var result = new List<string>();

        foreach (var r in refs)
        {
            // "all" → entire player's squad
            if (r.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return _unitRegistry.GetAllUnits()
                    .Where(u => u.SameGroup)
                    .Select(u => u.NetId)
                    .ToList();
            }

            // Team color → RPC to get members
            var team = r.ToUpperInvariant() switch
            {
                "RED" or "GREEN" or "BLUE" or "YELLOW" or "WHITE" => r.ToUpperInvariant(),
                _ => null
            };
            if (team != null)
            {
                var members = await ResolveTeamAsync(team);
                result.AddRange(members);
                continue;
            }

            // Looks like a netId (contains ':') → use directly
            if (r.Contains(':'))
            {
                result.Add(r);
                continue;
            }

            // Fallback: LLM returned a name — fuzzy match in registry
            var unit = _unitRegistry.FindByName(r);
            if (unit != null)
            {
                result.Add(unit.NetId);
            }
            else
            {
                Console.WriteLine($"[CommandExecutor] Could not resolve unit: \"{r}\"");
            }
        }

        return result;
    }

    /// <summary>
    /// Resolve a single unit reference (netId or name) — used for attack target and dialogue target.
    /// </summary>
    private string? ResolveUnitRef(string? r)
    {
        if (string.IsNullOrEmpty(r)) return null;
        if (r.Contains(':')) return r; // already a netId
        return _unitRegistry.FindByName(r)?.NetId;
    }

    private async Task<List<string>> ResolveTeamAsync(string teamColor)
    {
        try
        {
            var result = await _rpc.CallAsync($"['{teamColor}'] call arma3_mic_fnc_getTeamMembers");
            var cleaned = result.Trim('[', ']', '"', ' ');
            if (string.IsNullOrEmpty(cleaned)) return [];
            return cleaned.Split(',')
                .Select(s => s.Trim().Trim('"'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CommandExecutor] Failed to resolve team {teamColor}: {ex.Message}");
            return [];
        }
    }

    // ── Location resolution ──────────────────────────────────
    // LLM returns structured LocationParsed. C# only computes world coordinates.

    private float[] ResolveLocation(LocationParsed? loc, float[] lookTarget)
    {
        if (loc == null || loc.Type == "look_target")
            return lookTarget;

        if (loc.Type == "relative" && loc.Distance.HasValue && !string.IsNullOrEmpty(loc.Direction))
            return ComputeRelative(loc.Distance.Value, loc.Direction);

        if (loc.Type == "azimuth" && loc.Distance.HasValue && loc.Azimuth.HasValue)
            return ComputeAzimuth(loc.Distance.Value, loc.Azimuth.Value);

        return lookTarget;
    }

    private float[] ComputeRelative(float distance, string direction)
    {
        var pos = _gameState.PlayerPos;
        var playerDir = _gameState.PlayerDir * MathF.PI / 180f;

        float bearing = direction.ToLowerInvariant() switch
        {
            "forward" or "front" or "ahead" => playerDir,
            "back" or "backward" or "behind" => playerDir + MathF.PI,
            "left" => playerDir - MathF.PI / 2f,
            "right" => playerDir + MathF.PI / 2f,
            "north" => 0f,
            "south" => MathF.PI,
            "east" => MathF.PI / 2f,
            "west" => 3f * MathF.PI / 2f,
            _ => playerDir
        };

        return [pos[0] + distance * MathF.Sin(bearing), pos[1] + distance * MathF.Cos(bearing), pos[2]];
    }

    private float[] ComputeAzimuth(float distance, float azimuthDeg)
    {
        var pos = _gameState.PlayerPos;
        var azRad = azimuthDeg * MathF.PI / 180f;
        return [pos[0] + distance * MathF.Sin(azRad), pos[1] + distance * MathF.Cos(azRad), pos[2]];
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string FmtIds(List<string> ids) => string.Join(",", ids.Select(id => $"'{id}'"));

    private static string FmtPos(float[] p) => p.Length >= 3
        ? string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", p[0], p[1], p[2])
        : "[0,0,0]";

    private static void Log(string action, string msg) => Console.WriteLine($"[Cmd] {action}: {msg}");
}
