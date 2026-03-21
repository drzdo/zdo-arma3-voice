params ["_pos"];
private _obj = nearestObject [_pos, "All"];
if (isNull _obj) exitWith { "" };
_obj call BIS_fnc_netId
