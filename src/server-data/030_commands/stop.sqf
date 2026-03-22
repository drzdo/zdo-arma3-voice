["stop",
"Cancel current action, stay put but remain responsive to new orders. Triggers: stop, freeze, halt.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_stop;
    [_units, "stop"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
