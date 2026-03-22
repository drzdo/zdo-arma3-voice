zdoArmaVoice_fnc_commandDrop = {
    params ["_args", "_lookAtPosition", "_units"];
    { (_x call BIS_fnc_objectFromNetId) setUnitPos "DOWN" } forEach _units
};
["drop",
"Go prone immediately. Triggers: hit the dirt, get down.",
"{}",
zdoArmaVoice_fnc_commandDrop] call zdoArmaVoice_fnc_coreRegisterCommand
