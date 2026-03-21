params ["_netIds", "_vehicle"];
private _veh = nearestObject [_vehicle, "AllVehicles"];
if (isNull _veh) then { _veh = nearestObject [_vehicle, "Car"]; };
if (isNull _veh) exitWith { "no vehicle" };
{ (_x call BIS_fnc_objectFromNetId) assignAsCargo _veh; [(_x call BIS_fnc_objectFromNetId)] orderGetIn true } forEach _netIds;
"ok"
