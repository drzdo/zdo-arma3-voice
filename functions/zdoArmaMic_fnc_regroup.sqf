params ["_netIds"];
{ private _u = objectFromNetId _x; _u enableAI "MOVE"; _u doFollow player } forEach _netIds;
"ok"
