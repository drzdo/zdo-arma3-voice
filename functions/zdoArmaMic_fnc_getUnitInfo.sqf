params ["_netId"];
private _unit = objectFromNetId _netId;
[name _unit, str side _unit, group _unit == group player, typeOf _unit, rankId _unit]
