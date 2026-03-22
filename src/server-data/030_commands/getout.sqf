["getout",
"Get out of current vehicle. Triggers: get out, dismount.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_getOut
}] call zdoArmaVoice_fnc_coreRegisterCommand
