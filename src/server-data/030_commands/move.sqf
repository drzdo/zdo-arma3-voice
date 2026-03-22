zdoArmaVoice_fnc_commandMove = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        _u doMove _pos;
        _u setVariable ["zdoArmaVoice_toldMoveAt", time]
    } forEach _units;
    [_units, "move"] call zdoArmaVoice_fnc_buildAckInstruction
};
["move",
"Move units to a position. Triggers: go there, move to that building, 100 meters forward.",
"{position: Position}",
zdoArmaVoice_fnc_commandMove] call zdoArmaVoice_fnc_coreRegisterCommand
