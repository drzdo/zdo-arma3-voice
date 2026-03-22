["holdfire",
"Order units to hold fire, do not engage. Triggers: hold fire, cease fire.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_holdFire
}] call zdoArmaVoice_fnc_coreRegisterCommand
