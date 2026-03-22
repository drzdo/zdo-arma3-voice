zdoArmaVoice_fnc_determineTargetPosition = {
if (visibleMap) exitWith { screenToWorld getMousePosition };
private _start = eyePos player;
private _dir = if (currentWeapon player != "") then {
    player weaponDirection currentWeapon player
} else {
    eyeDirection player
};
private _end = _start vectorAdd (_dir vectorMultiply 3000);
private _intersect = lineIntersectsSurfaces [_start, _end, player, objNull, true, 1, "GEOM", "NONE"];
if (count _intersect > 0) exitWith { ASLToAGL ((_intersect select 0) select 0) };
screenToWorld [0.5, 0.5]
}
