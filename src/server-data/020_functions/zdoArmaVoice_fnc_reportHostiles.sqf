zdoArmaVoice_fnc_reportHostiles = {
params ["_netIds"];
private _allTargets = [];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _uPos = getPosASL _u;
    private _uDir = getDirVisual _u;
    private _uSide = side _u;
    private _known = _u targets [true, 600, [], 0];

    diag_log format ["ArmaVoice reportHostiles: unit=%1 side=%2 knownTargets=%3", name _u, _uSide, count _known];

    {
        _x params ["_target", "_lastKnownPos"];
        private _tSide = side _target;
        private _isEnemy = [_uSide, _tSide] call BIS_fnc_sideIsEnemy;

        diag_log format ["ArmaVoice reportHostiles: target=%1 side=%2 isEnemy=%3 alive=%4", typeOf _target, _tSide, _isEnemy, alive _target];

        if (_isEnemy && alive _target) then {
            private _dist = round (_uPos distance2D _lastKnownPos);
            private _dx = (_lastKnownPos select 0) - (_uPos select 0);
            private _dy = (_lastKnownPos select 1) - (_uPos select 1);
            private _bearing = round ((_dx atan2 _dy + 360) mod 360);
            private _relBearing = round ((_bearing - _uDir + 360) mod 360);
            private _typeName = getText (configFile >> "CfgVehicles" >> typeOf _target >> "displayName");
            if (_typeName == "") then { _typeName = typeOf _target };
            _allTargets pushBack [_typeName, str _tSide, _dist, _bearing, _relBearing];
        };
    } forEach _known;
} forEach _netIds;

diag_log format ["ArmaVoice reportHostiles: total enemies found=%1", count _allTargets];

_allTargets sort true;
_allTargets select [0, count _allTargets min 8]
}
