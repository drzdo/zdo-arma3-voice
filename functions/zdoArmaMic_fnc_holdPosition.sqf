params ["_netIds"];
{ private _u = objectFromNetId _x; doStop _u; _u disableAI "MOVE" } forEach _netIds;
"ok"
