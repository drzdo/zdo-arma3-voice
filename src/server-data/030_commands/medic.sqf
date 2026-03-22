if (isClass (configFile >> "CfgPatches" >> "ace_medical_ai")) then {
["medic",
"Request a medic via ACE3. Triggers: medic!, need a medic. If player requests for themselves, set units to []. If for someone else, set units to that unit's netId.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", []]] call zdoArmaVoice_fnc_resolveUnits;
    [_units] call zdoArmaVoice_fnc_requestMedic
}] call zdoArmaVoice_fnc_coreRegisterCommand
}
