params ["_netIds", "_behaviour"];
{ (_x call BIS_fnc_objectFromNetId) setBehaviour _behaviour } forEach _netIds;
"ok"
