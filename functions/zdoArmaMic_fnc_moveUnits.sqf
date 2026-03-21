params ["_netIds", "_pos"];
{ (_x call BIS_fnc_objectFromNetId) doMove _pos } forEach _netIds;
"ok"
