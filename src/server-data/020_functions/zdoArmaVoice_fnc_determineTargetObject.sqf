zdoArmaVoice_fnc_determineTargetObject = {
    params ["_pos", ["_radius", 5]];
    private _objects = nearestObjects [_pos, ["AllVehicles", "Man", "Building"], _radius];
    if (count _objects > 0) then { _objects select 0 } else { objNull }
}
