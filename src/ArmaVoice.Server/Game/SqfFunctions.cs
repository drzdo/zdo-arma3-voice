namespace ArmaVoice.Server.Game;

/// <summary>
/// SQF function definitions registered in-game via compileFinal.
/// Return values are serialized by toJSON in the SQF poll handler — do NOT use str.
/// Exception: use str for SQF types that toJSON can't handle (Side, Group, etc).
/// </summary>
public static class SqfFunctions
{
    public static readonly Dictionary<string, string> Functions = new()
    {
        ["arma3_mic_fnc_getUnitInfo"] =
            """params ["_netId"]; private _unit = objectFromNetId _netId; [name _unit, str side _unit, group _unit == group player, typeOf _unit, rankId _unit]""",

        ["arma3_mic_fnc_moveUnits"] =
            """params ["_netIds", "_pos"]; { (objectFromNetId _x) doMove _pos } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_attackTarget"] =
            """params ["_netIds", "_targetNetId"]; private _t = objectFromNetId _targetNetId; { (objectFromNetId _x) doFire _t } forEach _netIds; "ok" """,

        ["arma3_mic_fnc_stop"] =
            """params ["_netIds"]; { doStop (objectFromNetId _x) } forEach _netIds; "ok" """,

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
            """params ["_team"]; units group player select { assignedTeam _x == _team } apply { _x call BIS_fnc_netId }""",

        ["arma3_mic_fnc_getSquad"] =
            """(units group player - [player]) apply { [_x call BIS_fnc_netId, name _x, str side _x, typeOf _x, rankId _x, getPosASL _x, assignedTeam _x] }""",
    };

    public static void RegisterAll(RpcClient rpc)
    {
        Console.WriteLine($"[SqfFunctions] Registering {Functions.Count} functions...");

        foreach (var (name, body) in Functions)
        {
            var sqf = $"{name} = compileFinal '{body}'";
            rpc.Fire(sqf);
            Console.WriteLine($"[SqfFunctions]   Registered {name}");
        }

        Console.WriteLine("[SqfFunctions] All functions registered.");
    }
}
