zdoArmaVoice_fnc_commandGarrison = {
    params ["_args", "_lookAtPosition", "_units"];
    private _building = nearestObject [_lookAtPosition, "House"];
    if (isNull _building) exitWith { systemChat "No building nearby" };
    private _positions = _building buildingPos -1;
    if (count _positions == 0) exitWith { systemChat "Building has no positions" };
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _idx = _forEachIndex min (count _positions - 1);
        _u doMove (_positions select _idx)
    } forEach _units;
    [_units, "garrison"] call zdoArmaVoice_fnc_buildAckInstruction
};
["garrison",
"Order units to enter the building at crosshair. Triggers: garrison, enter the building, занять здание, в здание, зайди в дом.",
"{}",
zdoArmaVoice_fnc_commandGarrison] call zdoArmaVoice_fnc_coreRegisterCommand
