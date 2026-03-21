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
    private readonly DialogueManager? _dialogueManager;
    private readonly ILlmClient _llm;
    private readonly DialogueManager? _ackDialogue;
    private readonly float _ackChance;
    private readonly Random _rng = new();
    private List<string> _lastUnits = [];
    /// <summary>Player-assigned names → netId. E.g. "alpha" → "0:15" (a vehicle).</summary>
    private readonly Dictionary<string, string> _namedObjects = new(StringComparer.OrdinalIgnoreCase);

    public CommandExecutor(RpcClient rpc, UnitRegistry unitRegistry, GameState gameState,
        CommandRegistry commandRegistry, DialogueManager? dialogueManager, ILlmClient llm,
        float ackChance = 0f)
    {
        _rpc = rpc;
        _unitRegistry = unitRegistry;
        _gameState = gameState;
        _commandRegistry = commandRegistry;
        _dialogueManager = dialogueManager;
        _ackDialogue = dialogueManager;
        _llm = llm;
        _ackChance = Math.Clamp(ackChance, 0f, 1f);
    }

    public async Task ExecuteAsync(IntentParsed intent, float[] lookTarget)
    {
        var actionId = intent.Action.ToLowerInvariant();

        // Dialogue — handled by DialogueManager
        if (actionId == "dialogue")
        {
            if (_dialogueManager == null) { LogCmd("Dialogue", "disabled"); return; }
            var npcNetId = ResolveUnitRef(intent.Target);
            if (npcNetId == null) { LogCmd("Dialogue", $"target \"{intent.Target}\" not found."); return; }
            _dialogueManager.Enqueue(npcNetId, intent.Text ?? "");
            LogCmd("Dialogue", $"queued → {npcNetId}");
            return;
        }

        // Mark — create a map marker at look target
        if (actionId == "mark")
        {
            var label = (intent.Text ?? "Mark").Replace("'", "");
            var markPos = FmtPos(lookTarget);
            _rpc.Fire($"[{markPos}, '{label}'] call zdoArmaMic_fnc_createMarker");
            LogCmd("Mark", $"\"{label}\" at {markPos}");
            return;
        }

        // Sitrep hostiles — fetch data, let LLM compose a natural report, play via TTS
        if (actionId == "sitrep_hostiles")
        {
            var sitrepUnits = await ResolveUnitsAsync(intent.Units);
            if (sitrepUnits.Count == 0) { LogCmd("SitrepHostiles", "no units"); return; }
            if (_dialogueManager == null) { LogCmd("SitrepHostiles", "dialogue disabled"); return; }

            var reporterNetId = sitrepUnits[0];
            var reporter = _unitRegistry.GetUnit(reporterNetId);
            var reporterName = reporter?.Name ?? "Soldier";

            try
            {
                var sitrepArr = string.Join(",", sitrepUnits.Select(id => $"'{id}'"));
                var hostilesJson = await _rpc.CallAsync($"[[{sitrepArr}]] call zdoArmaMic_fnc_reportHostiles");

                // Send hostiles data to dialogue manager as a special report prompt
                var prompt = $"[SITREP] You are {reporterName}. Report these known hostile contacts to the commander in a brief, natural military radio style. "
                    + $"Data (format: [type, side, distance_m, absolute_bearing, relative_bearing_from_you]): {hostilesJson}. "
                    + "If the list is empty, say you don't see any hostiles. Be concise — 1-3 sentences. Use relative directions (ahead, left, right, behind) when helpful.";

                _dialogueManager.Enqueue(reporterNetId, prompt);
                LogCmd("SitrepHostiles", $"queued report from {reporterName}");
            }
            catch (Exception ex)
            {
                Log.Error("Cmd", $"Sitrep hostiles failed: {ex.Message}");
            }
            return;
        }

        // Sitrep position — unit reports where they are relative to player
        if (actionId == "sitrep_position")
        {
            var posUnits = await ResolveUnitsAsync(intent.Units);
            if (posUnits.Count == 0) { LogCmd("SitrepPos", "no units"); return; }
            if (_dialogueManager == null) { LogCmd("SitrepPos", "dialogue disabled"); return; }

            var reporterNetId = posUnits[0];
            try
            {
                var posArr = string.Join(",", posUnits.Select(id => $"'{id}'"));
                var posDataJson = await _rpc.CallAsync($"[[{posArr}]] call zdoArmaMic_fnc_getUnitPosition");
                var prompt = $"[POSITION REPORT] Report your position to the player. Player is {_gameState.PlayerRank} {_gameState.PlayerName}. "
                    + $"Data (format: [unitName, distance_m, bearing_degrees]): {posDataJson}. "
                    + "Be brief and natural, use relative directions. Address the player by rank.";
                _dialogueManager.Enqueue(reporterNetId, prompt);
                LogCmd("SitrepPos", $"queued from {reporterNetId}");
            }
            catch (Exception ex) { Log.Error("Cmd", $"Sitrep position failed: {ex.Message}"); }
            return;
        }

        // Name — assign a label to the object at look target
        if (actionId == "name")
        {
            var label = intent.Text ?? "";
            if (string.IsNullOrWhiteSpace(label)) { LogCmd("Name", "no name specified"); return; }
            try
            {
                var namePos = FmtPos(lookTarget);
                var netId = await _rpc.CallAsync($"[{namePos}] call zdoArmaMic_fnc_getObjectAt");
                netId = netId.Trim('"');
                if (string.IsNullOrEmpty(netId)) { LogCmd("Name", "no object at look target"); return; }
                _namedObjects[label] = netId;
                LogCmd("Name", $"\"{label}\" → {netId}");
                _rpc.Fire($"systemChat format ['Named object: %1 → %2', '{label.Replace("'", "")}', '{netId}']");
            }
            catch (Exception ex) { Log.Error("Cmd", $"Name failed: {ex.Message}"); }
            return;
        }

        // Look up command definition
        if (!_commandRegistry.Commands.TryGetValue(actionId, out var cmd))
        {
            LogCmd(actionId, "unknown command (not found in commands/)");
            return;
        }

        if (string.IsNullOrWhiteSpace(cmd.Sqf))
        {
            LogCmd(actionId, "command has no SQF");
            return;
        }

        // Resolve units — "last" means reuse previous target
        var netIds = await ResolveUnitsAsync(intent.Units);
        if (intent.Units is ["last"] && _lastUnits.Count > 0)
        {
            netIds = _lastUnits;
            Log.Info("Cmd", $"Using last units: [{string.Join(",", netIds)}]");
        }
        else if (netIds.Count > 0 && intent.Units is not ["last"])
        {
            _lastUnits = netIds;
        }
        var targetNetId = ResolveUnitRef(intent.Target) ?? "";
        var position = await ResolveLocationAsync(intent.Location, lookTarget, netIds);
        var stance = intent.Stance ?? "";
        var speed = intent.Speed ?? "";
        var formation = intent.Formation ?? "";

        // Build SQF: set local vars, then execute command's SQF
        var unitsArr = string.Join(",", netIds.Select(id => $"'{id}'"));
        var posStr = FmtPos(position);

        var sqf = $"""
            private _units = [[{unitsArr}]] call zdoArmaMic_fnc_filterAlive;
            private _target = '{targetNetId}';
            private _stance = '{stance}';
            private _speed = '{speed}';
            private _formation = '{formation}';
            private _pos = {posStr};
            {cmd.Sqf.Trim()}
            """;

        _rpc.Fire(sqf);
        LogCmd(actionId, $"units=[{string.Join(",", netIds)}] target={targetNetId} pos={posStr} stance={stance} speed={speed}");

        // Voice acknowledgment
        if (_ackChance > 0 && _ackDialogue != null && netIds.Count > 0 && _rng.NextSingle() < _ackChance)
        {
            // Pick one random unit to ack
            var ackNetId = netIds[_rng.Next(netIds.Count)];
            var ackUnit = _unitRegistry.GetUnit(ackNetId);
            if (ackUnit != null && !string.IsNullOrEmpty(ackUnit.Name))
            {
                _ackDialogue.Enqueue(ackNetId,
                    $"[ACK] The player ({_gameState.PlayerRank} {_gameState.PlayerName}) just gave you a '{actionId}' command. Respond with a very short military acknowledgment (1 sentence max). Address the player by rank. Examples: 'Так точно, сержант!', 'Есть, товарищ сержант!', 'Принял, {_gameState.PlayerRank}!', 'Roger that, sergeant!'");
                Log.Info("Cmd", $"Ack from {ackUnit.Name}");
            }
        }
    }

    // ── Unit resolution ──────────────────────────────────

    private async Task<List<string>> ResolveUnitsAsync(List<string> refs)
    {
        var result = new List<string>();
        foreach (var r in refs)
        {
            if (r.Equals("last", StringComparison.OrdinalIgnoreCase))
                return []; // handled by caller

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
            else Log.Warn("Cmd", $"Could not resolve unit: \"{r}\"");
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
            Log.Error("Cmd", $"Failed to resolve team {teamColor}: {ex.Message}");
            return [];
        }
    }

    // ── Location resolution ──────────────────────────────

    private async Task<float[]> ResolveLocationAsync(LocationParsed? loc, float[] lookTarget, List<string> unitNetIds)
    {
        if (loc == null || loc.Type == "look_target") return lookTarget;

        if (loc.Type == "relative" && loc.Distance.HasValue && !string.IsNullOrEmpty(loc.Direction))
        {
            var playerPos = _gameState.PlayerPos;
            var targetPos = ComputeRelative(loc.Distance.Value, loc.Direction, playerPos);

            // If units are already near the target (<2m), compute from unit centroid instead
            var centroid = GetUnitCentroid(unitNetIds);
            if (centroid != null)
            {
                float dx = centroid[0] - targetPos[0];
                float dy = centroid[1] - targetPos[1];
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < 2f)
                {
                    targetPos = ComputeRelative(loc.Distance.Value, loc.Direction, centroid);
                    Log.Info("Cmd", "Relative from unit position (already near target)");
                }
            }

            return targetPos;
        }

        if (loc.Type == "azimuth" && loc.Distance.HasValue && loc.Azimuth.HasValue)
            return ComputeAzimuth(loc.Distance.Value, loc.Azimuth.Value);
        if (loc.Type == "marker" && !string.IsNullOrEmpty(loc.Marker))
            return await ResolveMarkerAsync(loc.Marker) ?? lookTarget;
        if (loc.Type == "named" && !string.IsNullOrEmpty(loc.Marker))
            return await ResolveNamedObjectPosAsync(loc.Marker) ?? lookTarget;
        return lookTarget;
    }

    private float[]? GetUnitCentroid(List<string> netIds)
    {
        if (netIds.Count == 0) return null;

        float sumX = 0, sumY = 0, sumZ = 0;
        int count = 0;
        foreach (var netId in netIds)
        {
            var unit = _unitRegistry.GetUnit(netId);
            if (unit != null)
            {
                sumX += unit.Position[0];
                sumY += unit.Position[1];
                sumZ += unit.Position.Length > 2 ? unit.Position[2] : 0;
                count++;
            }
        }

        if (count == 0) return null;
        return [sumX / count, sumY / count, sumZ / count];
    }

    private async Task<float[]?> ResolveMarkerAsync(string playerQuery)
    {
        try
        {
            // 1. Get all markers from game
            var markersJson = await _rpc.CallAsync("call zdoArmaMic_fnc_getMarkers");
            if (string.IsNullOrEmpty(markersJson) || markersJson == "null" || markersJson == "[]")
            {
                Log.Warn("Cmd", "No markers on map");
                return null;
            }

            // 2. Ask LLM to pick the right marker
            var prompt = $"""
                The player said something about a marker: "{playerQuery}"
                Here are all map markers (format: [markerId, markerDisplayName]):
                {markersJson}

                Return ONLY the markerId (first element) that best matches what the player said.
                Return just the string, nothing else. If no match, return "none".
                """;

            var messages = new List<LlmMessage> { new("user", prompt) };
            var markerId = await _llm.CompleteAsync("You match marker names. Return only the markerId string.", messages, temperature: 0f, maxTokens: 50);

            if (string.IsNullOrEmpty(markerId) || markerId == "none")
            {
                Log.Warn("Cmd", $"LLM could not match marker for: '{playerQuery}'");
                return null;
            }

            markerId = markerId.Trim().Trim('"');
            Log.Info("Cmd", $"Marker resolved: '{playerQuery}' → markerId='{markerId}'");

            // 3. Get marker position
            var escaped = markerId.Replace("'", "");
            var posJson = await _rpc.CallAsync($"['{escaped}'] call zdoArmaMic_fnc_getMarkerPos");
            using var doc = System.Text.Json.JsonDocument.Parse(posJson);
            var arr = doc.RootElement;
            if (arr.ValueKind == System.Text.Json.JsonValueKind.Array && arr.GetArrayLength() >= 2)
            {
                var pos = new float[] { arr[0].GetSingle(), arr[1].GetSingle(), arr.GetArrayLength() >= 3 ? arr[2].GetSingle() : 0f };
                Log.Info("Cmd", $"Marker '{markerId}' position: {FmtPos(pos)}");
                return pos;
            }

            Log.Warn("Cmd", $"Marker '{markerId}' has no position");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Cmd", $"Marker resolve failed: {ex.Message}");
            return null;
        }
    }

    private async Task<float[]?> ResolveNamedObjectPosAsync(string name)
    {
        if (!_namedObjects.TryGetValue(name, out var netId))
        {
            Log.Warn("Cmd", $"Named object '{name}' not found in memory");
            return null;
        }

        try
        {
            var result = await _rpc.CallAsync($"getPosASL ('{netId}' call BIS_fnc_objectFromNetId)");
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            var arr = doc.RootElement;
            if (arr.ValueKind == System.Text.Json.JsonValueKind.Array && arr.GetArrayLength() >= 2)
                return [arr[0].GetSingle(), arr[1].GetSingle(), arr.GetArrayLength() >= 3 ? arr[2].GetSingle() : 0f];
        }
        catch (Exception ex) { Log.Error("Cmd", $"Named object pos failed: {ex.Message}"); }
        return null;
    }

    private float[] ComputeRelative(float distance, string direction, float[]? fromPos = null)
    {
        var pos = fromPos ?? _gameState.PlayerPos;
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

    private static void LogCmd(string action, string msg) => Log.Info("Cmd", $"{action}: {msg}");
}
