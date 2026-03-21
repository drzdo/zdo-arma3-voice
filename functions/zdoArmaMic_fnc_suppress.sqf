params ["_netIds", "_pos"];
private _units = _netIds apply { _x call BIS_fnc_objectFromNetId };
_units commandSuppressiveFire _pos;
"ok"
