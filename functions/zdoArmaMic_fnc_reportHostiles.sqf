params ["_netIds"];
private _allTargets = [];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _uPos = getPosASL _u;
    private _uDir = getDirVisual _u;
    private _known = _u targets [true, 0, [], 0];

    {
        _x params ["_target", "_lastKnownPos"];
        if (side _target != side _u && alive _target) then {
            private _dist = round (_uPos distance2D _lastKnownPos);
            private _dx = (_lastKnownPos select 0) - (_uPos select 0);
            private _dy = (_lastKnownPos select 1) - (_uPos select 1);
            private _bearing = round ((_dx atan2 _dy + 360) mod 360);
            private _relBearing = round ((_bearing - _uDir + 360) mod 360);
            private _typeName = getText (configFile >> "CfgVehicles" >> typeOf _target >> "displayName");
            if (_typeName == "") then { _typeName = typeOf _target };
            _allTargets pushBack [_typeName, str side _target, _dist, _bearing, _relBearing];
        };
    } forEach _known;
} forEach _netIds;

_allTargets sort true;
_allTargets select [0, count _allTargets min 8]
