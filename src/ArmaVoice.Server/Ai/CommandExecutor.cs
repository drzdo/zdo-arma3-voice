using System.Globalization;
using ArmaVoice.Server.Game;

namespace ArmaVoice.Server.Ai;

/// <summary>
/// Generic command executor. No hardcoded commands — reads from CommandRegistry.
/// Resolves units/targets/locations, then fires the command's SQF template.
/// </summary>
public class CommandExecutor
{
    private readonly RpcClient _rpc;
    private readonly UnitRegistry _unitRegistry;
    private readonly GameState _gameState;
    private readonly CommandRegistry _commandRegistry;
    private readonly DialogueManager _dialogueManager;

    public CommandExecutor(RpcClient rpc, UnitRegistry unitRegistry, GameState gameState,
        CommandRegistry commandRegistry, DialogueManager dialogueManager)
    {
        _rpc = rpc;
        _unitRegistry = unitRegistry;
        _gameState = gameState;
        _commandRegistry = commandRegistry;
        _dialogueManager = dialogueManager;
    }

    public async Task ExecuteAsync(IntentParsed intent, float[] lookTarget)
    {
        var actionId = intent.Action.ToLowerInvariant();

        // Dialogue is special — handled by DialogueManager, not SQF
        if (actionId == "dialogue")
        {
            var npcNetId = ResolveUnitRef(intent.Target);
            if (npcNetId == null) { Log("Dialogue", $"target \"{intent.Target}\" not found."); return; }
            _dialogueManager.Enqueue(npcNetId, intent.Text ?? "");
            Log("Dialogue", $"queued → {npcNetId}");
            return;
        }

        // Look up command definition
        if (!_commandRegistry.Commands.TryGetValue(actionId, out var cmd))
        {
            Log(actionId, "unknown command (not found in commands/)");
            return;
        }

        if (string.IsNullOrWhiteSpace(cmd.Sqf))
        {
            Log(actionId, "command has no SQF");
            return;
        }

        // Resolve standard params
        var netIds = await ResolveUnitsAsync(intent.Units);
        var targetNetId = ResolveUnitRef(intent.Target) ?? "";
        var position = ResolveLocation(intent.Location, lookTarget);
        var stance = intent.Stance ?? "";
        var speed = intent.Speed ?? "";
        var formation = intent.Formation ?? "";

        // Build SQF: set local vars, then execute command's SQF
        var unitsArr = string.Join(",", netIds.Select(id => $"'{id}'"));
        var posStr = FmtPos(position);

        var sqf = $"""
            private _units = [{unitsArr}];
            private _target = '{targetNetId}';
            private _stance = '{stance}';
            private _speed = '{speed}';
            private _formation = '{formation}';
            private _pos = {posStr};
            {cmd.Sqf.Trim()}
            """;

        _rpc.Fire(sqf);
        Log(actionId, $"units=[{string.Join(",", netIds)}] target={targetNetId} pos={posStr} stance={stance} speed={speed}");
    }

    // ── Unit resolution ──────────────────────────────────

    private async Task<List<string>> ResolveUnitsAsync(List<string> refs)
    {
        var result = new List<string>();
        foreach (var r in refs)
        {
            if (r.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return _unitRegistry.GetAllUnits()
                    .Where(u => u.SameGroup)
                    .Select(u => u.NetId)
                    .ToList();
            }

            var team = r.ToUpperInvariant();
            if (team is "RED" or "GREEN" or "BLUE" or "YELLOW" or "WHITE")
            {
                var members = await ResolveTeamAsync(team);
                result.AddRange(members);
                continue;
            }

            if (r.Contains(':')) { result.Add(r); continue; }

            var unit = _unitRegistry.FindByName(r);
            if (unit != null) result.Add(unit.NetId);
            else Console.WriteLine($"[Cmd] Could not resolve unit: \"{r}\"");
        }
        return result;
    }

    private string? ResolveUnitRef(string? r)
    {
        if (string.IsNullOrEmpty(r)) return null;
        if (r.Contains(':')) return r;
        return _unitRegistry.FindByName(r)?.NetId;
    }

    private async Task<List<string>> ResolveTeamAsync(string teamColor)
    {
        try
        {
            var result = await _rpc.CallAsync($"['{teamColor}'] call zdoArmaMic_fnc_getTeamMembers");
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            var arr = doc.RootElement;
            if (arr.ValueKind != System.Text.Json.JsonValueKind.Array) return [];
            return arr.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Cmd] Failed to resolve team {teamColor}: {ex.Message}");
            return [];
        }
    }

    // ── Location resolution ──────────────────────────────

    private float[] ResolveLocation(LocationParsed? loc, float[] lookTarget)
    {
        if (loc == null || loc.Type == "look_target") return lookTarget;
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
            "north" => 0f, "south" => MathF.PI,
            "east" => MathF.PI / 2f, "west" => 3f * MathF.PI / 2f,
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

    // ── Helpers ──────────────────────────────────────────

    private static string FmtPos(float[] p) => p.Length >= 3
        ? string.Format(CultureInfo.InvariantCulture, "[{0:F1},{1:F1},{2:F1}]", p[0], p[1], p[2])
        : "[0,0,0]";

    private static void Log(string action, string msg) => Console.WriteLine($"[Cmd] {action}: {msg}");
}
