params ["_netIds"];
{ (_x call BIS_fnc_objectFromNetId) setCombatMode "RED" } forEach _netIds;
"ok"
