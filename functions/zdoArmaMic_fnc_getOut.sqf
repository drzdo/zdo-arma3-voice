params ["_netIds"];
{ private _u = _x call BIS_fnc_objectFromNetId; unassignVehicle _u; [_u] orderGetIn false } forEach _netIds;
"ok"
