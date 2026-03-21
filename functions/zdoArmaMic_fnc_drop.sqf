params ["_netIds"];
{ (objectFromNetId _x) setUnitPos "DOWN" } forEach _netIds;
"ok"
