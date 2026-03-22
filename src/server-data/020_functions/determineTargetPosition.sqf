zdoArmaVoice_fnc_determineTargetPosition = {
if (visibleMap) exitWith { screenToWorld getMousePosition };
if (cameraView == "GUNNER" || currentWeapon player != "") then {
    private _intersect = lineIntersectsSurfaces [
        AGLToASL positionCameraToWorld [0,0,0],
        AGLToASL positionCameraToWorld [0,0,1000],
        player
    ];
    if (count _intersect > 0) exitWith { ASLToAGL ((_intersect select 0) select 0) };
};
screenToWorld [0.5, 0.5]
}
