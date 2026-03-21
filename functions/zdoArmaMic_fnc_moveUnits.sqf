params ["_netIds", "_pos"];
{ (objectFromNetId _x) doMove _pos } forEach _netIds;
"ok"
