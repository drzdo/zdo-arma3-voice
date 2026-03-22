zdoArmaVoice_fnc_determineTargetPosition = {
if (visibleMap) exitWith { screenToWorld getMousePosition };
if (currentWeapon player != "") then {
    private _start = eyePos player;
    private _dir = player weaponDirection currentWeapon player;
    private _end = _start vectorAdd (_dir vectorMultiply 1000);
    private _intersect = lineIntersectsSurfaces [_start, _end, player];
    if (count _intersect > 0) exitWith { ASLToAGL ((_intersect select 0) select 0) };
};
screenToWorld [0.5, 0.5]
}
