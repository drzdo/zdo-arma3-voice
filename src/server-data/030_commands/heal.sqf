if (isClass (configFile >> "CfgPatches" >> "ace_medical_ai")) then {
["heal",
"Order units to heal themselves using ACE3 medical AI. Triggers: heal yourself, patch up.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_heal
}] call zdoArmaVoice_fnc_coreRegisterCommand
}
