zdoArmaVoice_fnc_commandCircle = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _radius = _args getOrDefault ["radius", 10];
    private _count = count _units;
    if (_count == 0) exitWith {};
    private _step = 360 / _count;
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _angle = _forEachIndex * _step;
        private _unitPos = [
            (_pos select 0) + _radius * sin _angle,
            (_pos select 1) + _radius * cos _angle,
            _pos select 2
        ];
        _u doMove _unitPos;
        _u setVariable ["zdoArmaVoice_toldMoveAt", time];
        [_u, _unitPos, _angle] spawn {
            params ["_u", "_targetPos", "_outwardAngle"];
            private _startTime = time;
            private _timeout = time + 60;
            waitUntil {
                sleep 1;
                ([_u, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand)
                || { _u distance2D _targetPos < 3 }
                || { time > _timeout }
            };
            if (!([_u, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand) && { _u distance2D _targetPos < 3 }) then {
                private _watchPos = [
                    (getPosATL _u select 0) + 100 * sin _outwardAngle,
                    (getPosATL _u select 1) + 100 * cos _outwardAngle,
                    getPosATL _u select 2
                ];
                _u doWatch _watchPos
            }
        }
    } forEach _units;
    [_units, "circle"] call zdoArmaVoice_fnc_buildAckInstruction
};
["circle",
"Form a perimeter circle at a position. Units move to evenly spaced points on the circle and face outward. Triggers: form perimeter, circle up, set up perimeter, surround this position, встаньте в круг, периметр, оцепление.",
"{position?: Position, radius?: number (default 10)}",
zdoArmaVoice_fnc_commandCircle] call zdoArmaVoice_fnc_coreRegisterCommand
