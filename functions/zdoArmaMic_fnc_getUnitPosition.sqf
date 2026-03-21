params ["_netIds"];
private _playerPos = getPosASL player;
private _playerDir = getDirVisual player;
private _results = [];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _uPos = getPosASL _u;
    private _dist = round (_playerPos distance2D _uPos);
    private _dx = (_uPos select 0) - (_playerPos select 0);
    private _dy = (_uPos select 1) - (_playerPos select 1);
    private _bearing = round ((_dx atan2 _dy + 360) mod 360);
    _results pushBack [name _u, _dist, _bearing];
} forEach _netIds;
_results
