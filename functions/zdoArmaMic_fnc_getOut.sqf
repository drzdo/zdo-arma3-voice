params ["_netIds"];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    diag_log format ["ArmaVoice getOut: unit=%1 vehicle=%2", name _u, vehicle _u];
    if (vehicle _u != _u) then {
        moveOut _u;
    };
} forEach _netIds;
"ok"
