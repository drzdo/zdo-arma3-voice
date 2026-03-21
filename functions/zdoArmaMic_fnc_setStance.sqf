params ["_netIds", "_stance"];
{ (objectFromNetId _x) setUnitPos _stance } forEach _netIds;
"ok"
