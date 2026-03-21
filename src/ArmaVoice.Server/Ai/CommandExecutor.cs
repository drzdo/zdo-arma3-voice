using System.Globalization;
using System.Text.RegularExpressions;
using ArmaVoice.Server.Game;

namespace ArmaVoice.Server.Ai;

public class CommandExecutor
{
    private readonly RpcClient _rpc;
    private readonly UnitRegistry _unitRegistry;
    private readonly GameState _gameState;
    private readonly DialogueManager? _dialogueManager;

    public CommandExecutor(RpcClient rpc, UnitRegistry unitRegistry, GameState gameState, DialogueManager? dialogueManager)
    {
        _rpc = rpc;
        _unitRegistry = unitRegistry;
        _gameState = gameState;
        _dialogueManager = dialogueManager;
    }

    public async Task ExecuteAsync(IntentParsed intent, float[] lookTarget)
    {
        switch (intent.Action.ToLowerInvariant())
        {
            case "move":
                await ExecuteMoveAsync(intent, lookTarget);
                break;
            case "attack":
                await ExecuteAttackAsync(intent);
                break;
            case "hold":
                ExecuteWithUnits(intent, "hold");
                break;
            case "regroup":
                ExecuteWithUnits(intent, "regroup");
                break;
            case "formation":
                ExecuteFormation(intent);
                break;
            case "dialogue":
                ExecuteDialogue(intent);
                break;
            default:
                Console.WriteLine($"[CommandExecutor] Unknown action: {intent.Action}");
                break;
        }

        // Apply stance/speed modifiers if present (alongside any action)
        ApplyStance(intent);
        ApplySpeed(intent);
    }

    // ── Actions ──────────────────────────────────────────────

    private async Task ExecuteMoveAsync(IntentParsed intent, float[] lookTarget)
    {
        var netIds = await ResolveUnitsAsync(intent.Units);
        if (netIds.Count == 0) { Log("Move", "no units resolved."); return; }

        var position = ResolveLocation(intent.Location, lookTarget);
        var sqf = $"[[{FormatNetIdArray(netIds)}], {FormatPos(position)}] call arma3_mic_fnc_moveUnits";
        _rpc.Fire(sqf);
        Log("Move", $"{string.Join(", ", netIds)} to {FormatPos(position)}");
    }

    private async Task ExecuteAttackAsync(IntentParsed intent)
    {
        var netIds = await ResolveUnitsAsync(intent.Units);
        if (netIds.Count == 0) { Log("Attack", "no units resolved."); return; }

        if (string.IsNullOrEmpty(intent.Target))
        {
            Log("Attack", "no target specified.");
            return;
        }

        var targetUnit = _unitRegistry.FindByName(intent.Target);
        if (targetUnit == null)
        {
            Log("Attack", $"target \"{intent.Target}\" not found.");
            return;
        }

        var netIdList = FormatNetIdArray(netIds);
        _rpc.Fire($"[[{netIdList}], '{targetUnit.NetId}'] call arma3_mic_fnc_attackTarget");
        Log("Attack", $"{string.Join(", ", netIds)} engaging {targetUnit.Name}");
    }

    private async void ExecuteWithUnits(IntentParsed intent, string action)
    {
        var netIds = await ResolveUnitsAsync(intent.Units);
        if (netIds.Count == 0) { Log(action, "no units resolved."); return; }

        var funcName = action switch
        {
            "hold" => "arma3_mic_fnc_holdPosition",
            "regroup" => "arma3_mic_fnc_regroup",
            _ => null
        };

        if (funcName == null) return;

        _rpc.Fire($"[[{FormatNetIdArray(netIds)}]] call {funcName}");
        Log(action, string.Join(", ", netIds));
    }

    private void ExecuteFormation(IntentParsed intent)
    {
        var formation = MapFormation(intent.Formation ?? "");
        if (string.IsNullOrEmpty(formation))
        {
            Log("Formation", $"unknown \"{intent.Formation}\".");
            return;
        }

        _rpc.Fire($"['{formation}'] call arma3_mic_fnc_setFormation");
        Log("Formation", formation);
    }

    private void ExecuteDialogue(IntentParsed intent)
    {
        if (string.IsNullOrEmpty(intent.Target))
        {
            Log("Dialogue", "no target NPC specified.");
            return;
        }

        var targetUnit = _unitRegistry.FindByName(intent.Target);
        if (targetUnit == null)
        {
            Log("Dialogue", $"target \"{intent.Target}\" not found.");
            return;
        }

        if (_dialogueManager == null)
        {
            Log("Dialogue", "DialogueManager not available.");
            return;
        }

        _dialogueManager.Enqueue(targetUnit.NetId, intent.Text ?? intent.Target);
        Log("Dialogue", $"queued with {targetUnit.Name}");
    }

    // ── Stance / Speed modifiers ─────────────────────────────

    private async void ApplyStance(IntentParsed intent)
    {
        if (string.IsNullOrEmpty(intent.Stance)) return;

        var sqfStance = intent.Stance.ToLowerInvariant() switch
        {
            "prone" or "crawl" => "DOWN",
            "crouch" or "crouched" or "kneel" => "MIDDLE",
            "standing" or "stand" or "up" => "UP",
            "auto" => "AUTO",
            _ => null
        };

        if (sqfStance == null)
        {
            Log("Stance", $"unknown \"{intent.Stance}\".");
            return;
        }

        var netIds = await ResolveUnitsAsync(intent.Units);
        if (netIds.Count == 0) return;

        _rpc.Fire($"[[{FormatNetIdArray(netIds)}], '{sqfStance}'] call arma3_mic_fnc_setStance");
        Log("Stance", $"{sqfStance} for {string.Join(", ", netIds)}");
    }

    private void ApplySpeed(IntentParsed intent)
    {
        if (string.IsNullOrEmpty(intent.Speed)) return;

        var sqfSpeed = intent.Speed.ToLowerInvariant() switch
        {
            "sprint" or "fast" => "FULL",
            "run" or "normal" => "NORMAL",
            "walk" or "slow" or "slowly" => "LIMITED",
            _ => null
        };

        if (sqfSpeed == null)
        {
            Log("Speed", $"unknown \"{intent.Speed}\".");
            return;
        }

        _rpc.Fire($"['{sqfSpeed}'] call arma3_mic_fnc_setSpeed");
        Log("Speed", sqfSpeed);
    }

    // ── Unit resolution ──────────────────────────────────────

    private async Task<List<string>> ResolveUnitsAsync(List<string> unitRefs)
    {
        var netIds = new List<string>();

        foreach (var raw in unitRefs)
        {
            var r = raw.Trim().ToLowerInvariant();

            // "all" — entire squad
            if (r is "all" or "everyone" or "squad" or "все")
            {
                return _unitRegistry.GetAllUnits()
                    .Where(u => u.SameGroup)
                    .Select(u => u.NetId)
                    .ToList();
            }

            // Team color: "team_red", "red team", "red", etc.
            var teamColor = TryParseTeamColor(r);
            if (teamColor != null)
            {
                var members = await ResolveTeamAsync(teamColor);
                netIds.AddRange(members);
                continue;
            }

            // Unit by index: "unit 2", "unit_3"
            var indexMatch = Regex.Match(r, @"unit[_ ]?(\d+)");
            if (indexMatch.Success && int.TryParse(indexMatch.Groups[1].Value, out var idx))
            {
                // 1-based index into squad
                var allGroup = _unitRegistry.GetAllUnits()
                    .Where(u => u.SameGroup)
                    .ToList();

                if (idx >= 1 && idx <= allGroup.Count)
                {
                    netIds.Add(allGroup[idx - 1].NetId);
                }
                else
                {
                    Console.WriteLine($"[CommandExecutor] Unit index {idx} out of range (squad has {allGroup.Count}).");
                }
                continue;
            }

            // Name / role match
            var unit = _unitRegistry.FindByName(raw);
            if (unit != null)
            {
                netIds.Add(unit.NetId);
            }
            else
            {
                Console.WriteLine($"[CommandExecutor] Could not resolve unit: \"{raw}\"");
            }
        }

        return netIds;
    }

    private static string? TryParseTeamColor(string r)
    {
        if (r.Contains("red") || r.Contains("красн")) return "RED";
        if (r.Contains("green") || r.Contains("зелен")) return "GREEN";
        if (r.Contains("blue") || r.Contains("син")) return "BLUE";
        if (r.Contains("yellow") || r.Contains("желт")) return "YELLOW";
        if (r.Contains("white") || r.Contains("бел")) return "WHITE";
        return null;
    }

    private async Task<List<string>> ResolveTeamAsync(string teamColor)
    {
        try
        {
            var result = await _rpc.CallAsync($"['{teamColor}'] call arma3_mic_fnc_getTeamMembers");
            // Result is SQF array of netIds: ["2:3","2:7"]
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

    private float[] ResolveLocation(string? location, float[] lookTarget)
    {
        if (string.IsNullOrEmpty(location) || location == "look_target")
            return lookTarget;

        // Relative: "100m_forward", "50m_left", "200m_north", etc.
        var relMatch = Regex.Match(location, @"(\d+)m?_(forward|back|left|right|north|south|east|west)",
            RegexOptions.IgnoreCase);

        if (relMatch.Success)
        {
            var dist = float.Parse(relMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var dir = relMatch.Groups[2].Value.ToLowerInvariant();
            return ComputeRelativePosition(dist, dir);
        }

        // Try parse explicit coords
        return TryParsePosition(location) ?? lookTarget;
    }

    private float[] ComputeRelativePosition(float distance, string direction)
    {
        var pos = _gameState.PlayerPos;
        var playerDirRad = _gameState.PlayerDir * MathF.PI / 180f;

        // In Arma: 0° = north (+Y), 90° = east (+X)
        float bearing = direction switch
        {
            "forward" or "ahead" => playerDirRad,
            "back" or "backward" => playerDirRad + MathF.PI,
            "left" => playerDirRad - MathF.PI / 2f,
            "right" => playerDirRad + MathF.PI / 2f,
            "north" => 0f,
            "south" => MathF.PI,
            "east" => MathF.PI / 2f,
            "west" => 3f * MathF.PI / 2f,
            _ => playerDirRad
        };

        float dx = distance * MathF.Sin(bearing);
        float dy = distance * MathF.Cos(bearing);

        return [pos[0] + dx, pos[1] + dy, pos[2]];
    }

    // ── Helpers ───────────────────────────────────────────────

    private static string FormatNetIdArray(List<string> netIds)
        => string.Join(",", netIds.Select(id => $"'{id}'"));

    private static string FormatPos(float[] pos)
    {
        if (pos.Length >= 3)
            return string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", pos[0], pos[1], pos[2]);
        if (pos.Length == 2)
            return string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},0]", pos[0], pos[1]);
        return "[0,0,0]";
    }

    private static float[]? TryParsePosition(string input)
    {
        var s = input.Trim().Trim('[', ']');
        var parts = s.Split(',');
        if (parts.Length < 2) return null;

        var result = new float[3];
        for (int i = 0; i < Math.Min(parts.Length, 3); i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                return null;
        }
        return result;
    }

    private static string MapFormation(string formation) => formation.ToLowerInvariant().Trim() switch
    {
        "column" => "COLUMN",
        "line" => "LINE",
        "wedge" => "WEDGE",
        "vee" or "v" => "VEE",
        "staggered column" or "staggered" => "STAG COLUMN",
        "diamond" => "DIAMOND",
        "file" => "FILE",
        "echelon left" => "ECH LEFT",
        "echelon right" => "ECH RIGHT",
        _ => ""
    };

    private static void Log(string action, string msg)
        => Console.WriteLine($"[CommandExecutor] {action}: {msg}");
}
