zdoArmaVoice_fnc_commandRegroup = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        _u doFollow player;
        _u setVariable ["zdoArmaVoice_toldRegroupAt", time]
    } forEach _units;
    [_units, "regroup"] call zdoArmaVoice_fnc_buildAckInstruction
};
["regroup",
"Regroup on the player, come back. Triggers: regroup, come to me, fall back.",
"{}",
zdoArmaVoice_fnc_commandRegroup] call zdoArmaVoice_fnc_coreRegisterCommand
