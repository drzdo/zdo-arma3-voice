["openfire",
"Allow units to engage freely. Triggers: open fire, weapons free, fire at will.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_openFire
}] call zdoArmaVoice_fnc_coreRegisterCommand
