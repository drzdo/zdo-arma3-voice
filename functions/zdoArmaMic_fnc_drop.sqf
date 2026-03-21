params ["_netIds"];
{ (_x call BIS_fnc_objectFromNetId) setUnitPos "DOWN" } forEach _netIds;
"ok"
