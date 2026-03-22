["garrison",
"Order units to garrison/enter the nearest building at look target position. Triggers: garrison that building, enter the building.",
"{units: Units, position?: Position}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    [_units, _pos] call zdoArmaVoice_fnc_garrison;
    [_units, "garrison"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
