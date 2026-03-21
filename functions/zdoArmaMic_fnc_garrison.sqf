params ["_netIds", "_pos"];
private _building = nearestObject [_pos, "House"];
if (isNull _building) exitWith { "no building nearby" };
private _positions = _building buildingPos -1;
if (count _positions == 0) exitWith { "building has no positions" };
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _idx = _forEachIndex min (count _positions - 1);
    _u doMove (_positions select _idx);
} forEach _netIds;
"ok"
