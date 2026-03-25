zdoArmaVoice_fnc_commandSuppress = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    (_units apply { _x call BIS_fnc_objectFromNetId }) commandSuppressiveFire _pos;
    [_units, "suppress"] call zdoArmaVoice_fnc_buildAckInstruction
};
["suppress",
"Suppressive fire at a position or direction. Triggers: suppress that position, suppressive fire, подави, огонь на подавление.",
"{position?: Position}",
zdoArmaVoice_fnc_commandSuppress] call zdoArmaVoice_fnc_coreRegisterCommand
