params ["_netId"];
private _unit = _netId call BIS_fnc_objectFromNetId;
[name _unit, str side _unit, group _unit == group player, typeOf _unit, rankId _unit]
