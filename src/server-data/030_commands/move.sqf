zdoArmaVoice_fnc_commandMove = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _spacing = 5;
    private _count = count _units;
    private _playerPos = getPosATL player;
    private _dx = (_pos select 0) - (_playerPos select 0);
    private _dy = (_pos select 1) - (_playerPos select 1);
    private _bearing = _dx atan2 _dy;
    private _perpBearing = _bearing + 90;
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _offset = (_forEachIndex - (_count - 1) / 2) * _spacing;
        private _unitPos = [
            (_pos select 0) + _offset * sin _perpBearing,
            (_pos select 1) + _offset * cos _perpBearing,
            _pos select 2
        ];
        _u doMove _unitPos;
        _u setVariable ["zdoArmaVoice_toldMoveAt", time]
    } forEach _units;
    [_units, "move"] call zdoArmaVoice_fnc_buildAckInstruction
};
["move",
"Move units to a position. Also used for driving vehicles. Triggers: go there, move to that building, 100 meters forward, drive there, езжай туда.",
"{position: Position}",
zdoArmaVoice_fnc_commandMove] call zdoArmaVoice_fnc_coreRegisterCommand
