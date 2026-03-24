zdoArmaVoice_fnc_commandMedic = {
    params ["_args", "_lookAtPosition", "_units"];
    if (count _units == 0) then {
        player call ace_medical_ai_fnc_requestMedic
    } else {
        { (_x call BIS_fnc_objectFromNetId) call ace_medical_ai_fnc_requestMedic } forEach _units
    }
};
if (isClass (configFile >> "CfgPatches" >> "ace_medical_ai")) then {
["medic",
"Request a medic. Triggers: medic!, need a medic.",
"{}",
zdoArmaVoice_fnc_commandMedic] call zdoArmaVoice_fnc_coreRegisterCommand
}
