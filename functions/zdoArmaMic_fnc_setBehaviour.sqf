params ["_netIds", "_behaviour"];
private _groups = [];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _g = group _u;
    if !(_g in _groups) then {
        _groups pushBack _g;
        _g setBehaviour _behaviour;
    };
} forEach _netIds;
"ok"
