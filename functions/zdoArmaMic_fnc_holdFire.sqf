params ["_netIds"];
{ (_x call BIS_fnc_objectFromNetId) setCombatMode "BLUE" } forEach _netIds;
"ok"
