zdoArmaVoice_fnc_commandStance = {
    params ["_args", "_lookAtPosition", "_units"];
    private _stance = _args getOrDefault ["stance", "UP"];
    [_units, _stance] call zdoArmaVoice_fnc_setStance;
    [_units, "stance"] call zdoArmaVoice_fnc_buildAckInstruction
};
["stance",
"Change unit stance/posture. Triggers: stand up (UP), crouch (MIDDLE), prone (DOWN), auto (AUTO).",
"{stance: DOWN/MIDDLE/UP/AUTO}",
zdoArmaVoice_fnc_commandStance] call zdoArmaVoice_fnc_coreRegisterCommand
