zdoArmaVoice_fnc_commandRegroup = {
    params ["_args", "_lookAtPosition", "_units"];
    { (_x call BIS_fnc_objectFromNetId) doFollow player } forEach _units;
    [_units, "regroup"] call zdoArmaVoice_fnc_buildAckInstruction
};
["regroup",
"Regroup on the player, come back. Triggers: regroup, come to me, fall back.",
"{}",
zdoArmaVoice_fnc_commandRegroup] call zdoArmaVoice_fnc_coreRegisterCommand
