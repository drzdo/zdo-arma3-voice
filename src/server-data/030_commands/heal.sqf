zdoArmaVoice_fnc_commandHeal = {
    params ["_args", "_lookAtPosition", "_units"];
    { [_x call BIS_fnc_objectFromNetId] call ace_medical_ai_fnc_healSelf } forEach _units
};
if (isClass (configFile >> "CfgPatches" >> "ace_medical_ai")) then {
["heal",
"Order units to heal themselves using ACE3 medical AI. Triggers: heal yourself, patch up.",
"{}",
zdoArmaVoice_fnc_commandHeal] call zdoArmaVoice_fnc_coreRegisterCommand
}
