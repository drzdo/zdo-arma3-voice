zdoArmaVoice_fnc_commandHold = {
    params ["_args", "_lookAtPosition", "_units"];
    { doStop (_x call BIS_fnc_objectFromNetId) } forEach _units;
    [_units, "hold"] call zdoArmaVoice_fnc_buildAckInstruction
};
["hold",
"Stop and LOCK position. Units will not move until given new orders. Triggers: hold position.",
"{}",
zdoArmaVoice_fnc_commandHold] call zdoArmaVoice_fnc_coreRegisterCommand
