params ["_netIds", "_targetNetId"];
private _t = objectFromNetId _targetNetId;
{ (objectFromNetId _x) doFire _t } forEach _netIds;
"ok"
