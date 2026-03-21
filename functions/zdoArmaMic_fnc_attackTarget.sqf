params ["_netIds", "_targetNetId"];
private _t = _targetNetId call BIS_fnc_objectFromNetId;
{ (_x call BIS_fnc_objectFromNetId) doFire _t } forEach _netIds;
"ok"
