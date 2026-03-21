params ["_netIds", "_pos"];
private _veh = nearestObject [_pos, "LandVehicle"];
if (isNull _veh) then { _veh = nearestObject [_pos, "Air"] };
if (isNull _veh) then { _veh = nearestObject [_pos, "Ship"] };
if (isNull _veh) exitWith { "no vehicle" };
{
    private _u = _x call BIS_fnc_objectFromNetId;
    _u assignAsCargo _veh;
    [_u] orderGetIn true;
} forEach _netIds;
"ok"
