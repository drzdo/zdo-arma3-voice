["drop",
"Go prone immediately. Triggers: hit the dirt, get down.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_drop
}] call zdoArmaVoice_fnc_coreRegisterCommand
