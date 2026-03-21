params ["_netIds"];
{ doStop (objectFromNetId _x) } forEach _netIds;
"ok"
