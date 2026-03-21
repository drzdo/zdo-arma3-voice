namespace ArmaVoice.Server.Game;

public class UnitInfo
{
    public string NetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Side { get; set; } = "";
    public bool SameGroup { get; set; }
    public string UnitType { get; set; } = "";
    public int Rank { get; set; }
    public float[] Position { get; set; } = [0, 0, 0];
    public int LastSeenFrame { get; set; }
    public bool InfoLoaded { get; set; }
}

/// <summary>
/// Caches unit info by netId. Updates positions from per-frame state messages,
/// and fires async RPCs via RpcClient to load full info for newly discovered units.
/// </summary>
public class UnitRegistry
{
    private readonly Dictionary<string, UnitInfo> _units = new();
    private readonly RpcClient _rpc;
    private int _frameCounter;
    private readonly object _lock = new();

    public UnitRegistry(RpcClient rpc)
    {
        _rpc = rpc;
    }

    /// <summary>
    /// Update unit positions from a state message. Marks LastSeenFrame on each unit.
    /// Fires async RPCs for newly discovered netIds.
    /// </summary>
    public void UpdateFromState(List<(string NetId, float[] Pos)> units)
    {
        _frameCounter++;

        List<string>? newNetIds = null;

        lock (_lock)
        {
            foreach (var (netId, pos) in units)
            {
                if (_units.TryGetValue(netId, out var existing))
                {
                    existing.Position = pos;
                    existing.LastSeenFrame = _frameCounter;
                }
                else
                {
                    var info = new UnitInfo
                    {
                        NetId = netId,
                        Position = pos,
                        LastSeenFrame = _frameCounter,
                        InfoLoaded = false
                    };
                    _units[netId] = info;

                    newNetIds ??= [];
                    newNetIds.Add(netId);
                }
            }
        }

        // Fire info RPCs for new units outside the lock
        if (newNetIds != null)
        {
            foreach (var netId in newNetIds)
            {
                _ = LoadUnitInfoAsync(netId);
            }
        }
    }

    /// <summary>
    /// Get a unit by netId, or null if not found.
    /// </summary>
    public UnitInfo? GetUnit(string netId)
    {
        lock (_lock)
        {
            return _units.GetValueOrDefault(netId);
        }
    }

    /// <summary>
    /// Get a snapshot of all currently tracked units.
    /// </summary>
    public List<UnitInfo> GetAllUnits()
    {
        lock (_lock)
        {
            return [.. _units.Values];
        }
    }

    /// <summary>
    /// Find a unit by name using case-insensitive substring match.
    /// Returns the first match, or null.
    /// </summary>
    public UnitInfo? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            // Try exact case-insensitive match first
            foreach (var unit in _units.Values)
            {
                if (unit.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return unit;
            }

            // Fall back to contains match
            foreach (var unit in _units.Values)
            {
                if (unit.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return unit;
            }
        }

        return null;
    }

    /// <summary>
    /// Sync the full squad roster via RPC. Call on connect and periodically.
    /// Returns all squad members regardless of distance.
    /// </summary>
    public async Task SyncSquadAsync()
    {
        try
        {
            var result = await _rpc.CallAsync("call arma3_mic_fnc_getSquad");
            var parsed = SqfParser.Parse(result);
            if (parsed is not List<object> arr)
            {
                Console.WriteLine($"[UnitRegistry] Failed to parse squad data: {result[..Math.Min(80, result.Length)]}");
                return;
            }

            lock (_lock)
            {
                foreach (var item in arr)
                {
                    if (item is not List<object> unit || unit.Count < 7) continue;

                    var netId = unit[0]?.ToString() ?? "";
                    if (string.IsNullOrEmpty(netId)) continue;

                    if (!_units.TryGetValue(netId, out var info))
                    {
                        info = new UnitInfo { NetId = netId };
                        _units[netId] = info;
                    }

                    info.Name = unit[1]?.ToString() ?? "";
                    info.Side = unit[2]?.ToString() ?? "";
                    info.SameGroup = true;
                    info.UnitType = unit[3]?.ToString() ?? "";
                    info.Rank = unit[4] switch { double d => (int)d, int i => i, _ => 0 };

                    if (unit[5] is List<object> posArr && posArr.Count >= 3)
                    {
                        info.Position = [
                            posArr[0] is double px ? (float)px : 0f,
                            posArr[1] is double py ? (float)py : 0f,
                            posArr[2] is double pz ? (float)pz : 0f
                        ];
                    }

                    info.InfoLoaded = true;
                    info.LastSeenFrame = _frameCounter;
                }
            }

            Console.WriteLine($"[UnitRegistry] Squad synced: {arr.Count} members.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UnitRegistry] Squad sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Remove non-squad units that haven't been seen for the given number of frames.
    /// Squad members (SameGroup=true) are never evicted.
    /// </summary>
    public void EvictStale(int maxAge)
    {
        lock (_lock)
        {
            var staleIds = new List<string>();
            foreach (var (netId, info) in _units)
            {
                // Never evict squad members
                if (info.SameGroup) continue;

                if (_frameCounter - info.LastSeenFrame > maxAge)
                {
                    staleIds.Add(netId);
                }
            }

            foreach (var netId in staleIds)
            {
                _units.Remove(netId);
                Console.WriteLine($"[UnitRegistry] Evicted stale unit: {netId}");
            }
        }
    }

    /// <summary>
    /// Async RPC to load full info for a unit from the game.
    /// Calls arma3_mic_fnc_getUnitInfo and parses the result.
    /// </summary>
    private async Task LoadUnitInfoAsync(string netId)
    {
        try
        {
            // SQF call: 'netId' call arma3_mic_fnc_getUnitInfo
            // Returns: str [name, side, sameGroup, typeOf, rankId]
            // Which is: ["Sgt. Miller","WEST",true,"B_Soldier_F",3]
            var sqf = $"'{netId}' call arma3_mic_fnc_getUnitInfo";
            var result = await _rpc.CallAsync(sqf);

            // Parse the SQF array result
            var parsed = SqfParser.Parse(result);
            if (parsed is not List<object> arr || arr.Count < 5)
            {
                Console.WriteLine($"[UnitRegistry] Invalid unit info for {netId}: {result[..Math.Min(80, result.Length)]}");
                return;
            }

            lock (_lock)
            {
                if (!_units.TryGetValue(netId, out var info))
                    return; // Unit was evicted in the meantime

                info.Name = arr[0]?.ToString() ?? "";
                info.Side = arr[1]?.ToString() ?? "";
                info.SameGroup = arr[2] is true;
                info.UnitType = arr[3]?.ToString() ?? "";
                info.Rank = arr[4] switch
                {
                    double d => (int)d,
                    int i => i,
                    _ => 0
                };
                info.InfoLoaded = true;

                Console.WriteLine($"[UnitRegistry] Loaded info for {netId}: {info.Name} ({info.Side}, {info.UnitType})");
            }
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[UnitRegistry] Timeout loading info for {netId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UnitRegistry] Error loading info for {netId}: {ex.Message}");
        }
    }
}
