namespace ArmaVoice.Server.Game;

/// <summary>
/// SQF function definitions that get registered in-game via compileFinal.
/// Sent once on client connect as fire-and-forget RPCs (id=0).
/// </summary>
public static class SqfFunctions
{
    /// <summary>
    /// Map of function name to SQF body (without the outer quotes — those are added at registration).
    /// </summary>
    public static readonly Dictionary<string, string> Functions = new()
    {
        ["arma3_mic_fnc_getUnitInfo"] =
            """params ["_netId"]; private _unit = objectFromNetId _netId; str [name _unit, str side _unit, group _unit == group player, typeOf _unit, rankId _unit]""",

        ["arma3_mic_fnc_moveUnits"] =
            """params ["_netIds", "_pos"]; { (objectFromNetId _x) doMove _pos } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_attackTarget"] =
            """params ["_netIds", "_targetNetId"]; private _t = objectFromNetId _targetNetId; { (objectFromNetId _x) doFire _t } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_holdPosition"] =
            """params ["_netIds"]; { private _u = objectFromNetId _x; doStop _u; _u disableAI "MOVE" } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_regroup"] =
            """params ["_netIds"]; { private _u = objectFromNetId _x; _u enableAI "MOVE"; _u doFollow player } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_setFormation"] =
            """params ["_formation"]; group player setFormation _formation; "ok" """,

        ["arma3_mic_fnc_setStance"] =
            """params ["_netIds", "_stance"]; { (objectFromNetId _x) setUnitPos _stance } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_setSpeed"] =
            """params ["_speed"]; group player setSpeedMode _speed; "ok" """,

        ["arma3_mic_fnc_getTeamMembers"] =
            """params ["_team"]; str (units group player select { assignedTeam _x == _team } apply { _x call BIS_fnc_netId })""",
    };

    /// <summary>
    /// Register all SQF functions in the game by sending compileFinal calls as fire-and-forget RPCs.
    /// SQF single-quoted strings don't need internal escaping for double-quotes,
    /// but single quotes within the body would. Our function bodies contain only double-quoted
    /// strings, so wrapping in single quotes is safe.
    /// </summary>
    public static void RegisterAll(RpcClient rpc)
    {
        Console.WriteLine($"[SqfFunctions] Registering {Functions.Count} functions...");

        foreach (var (name, body) in Functions)
        {
            // SQF: funcName = compileFinal 'body'
            var sqf = $"{name} = compileFinal '{body}'";
            rpc.Fire(sqf);
            Console.WriteLine($"[SqfFunctions]   Registered {name}");
        }

        Console.WriteLine("[SqfFunctions] All functions registered.");
    }
}
