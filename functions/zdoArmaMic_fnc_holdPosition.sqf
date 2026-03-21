params ["_netIds"];
{ private _u = _x call BIS_fnc_objectFromNetId; doStop _u; _u disableAI "MOVE" } forEach _netIds;
"ok"
