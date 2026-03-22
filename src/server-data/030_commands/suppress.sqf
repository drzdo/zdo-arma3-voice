["suppress",
"Suppressive fire at a position or direction. Triggers: suppress that position, suppressive fire.",
"{units: Units, position?: Position}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    [_units, _pos] call zdoArmaVoice_fnc_suppress;
    [_units, "suppress"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
