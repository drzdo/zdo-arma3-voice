params ["_netIds", "_pos"];
private _building = nearestObject [_pos, "House"];
diag_log format ["ArmaVoice garrison: pos=%1 building=%2 isNull=%3", _pos, _building, isNull _building];
if (isNull _building) exitWith { "no building nearby" };
private _positions = _building buildingPos -1;
diag_log format ["ArmaVoice garrison: buildingPos count=%1", count _positions];
if (count _positions == 0) exitWith { "building has no positions" };
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _idx = _forEachIndex min (count _positions - 1);
    _u enableAI "MOVE";
    _u enableAI "PATH";
    _u doMove (_positions select _idx);
    diag_log format ["ArmaVoice garrison: %1 -> pos #%2", name _u, _idx];
} forEach _netIds;
"ok"
