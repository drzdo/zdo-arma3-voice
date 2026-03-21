params ["_netIds", "_pos", "_role"];
private _veh = nearestObject [_pos, "LandVehicle"];
if (isNull _veh) then { _veh = nearestObject [_pos, "Air"] };
if (isNull _veh) then { _veh = nearestObject [_pos, "Ship"] };
if (isNull _veh) exitWith { "no vehicle" };
{
    private _u = _x call BIS_fnc_objectFromNetId;
    switch (_role) do {
        case "driver": { _u moveInDriver _veh };
        case "gunner": { _u moveInGunner _veh };
        case "commander": { _u moveInCommander _veh };
        case "cargo": { _u moveInCargo _veh };
        default { _u moveInAny _veh };
    };
} forEach _netIds;
"ok"
