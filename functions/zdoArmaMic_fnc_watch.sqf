params ["_netIds", "_pos"];
private _units = _netIds apply { _x call BIS_fnc_objectFromNetId };
_units commandWatch _pos;
"ok"
