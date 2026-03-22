zdoArmaVoice_fnc_commandMirrorstance = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _unitNetId = _x;
        [_unitNetId] spawn {
            params ["_netId"];
            private _unit = _netId call BIS_fnc_objectFromNetId;
            private _startTime = time;
            private _lastStance = "";
            while {
                alive _unit
                && { (_unit getVariable ["zdoArmaVoice_toldStanceAt", 0]) <= _startTime }
            } do {
                private _playerStance = unitPos player;
                if (_playerStance != _lastStance) then {
                    _unit setUnitPos _playerStance;
                    _lastStance = _playerStance
                };
                sleep 0.5
            }
        }
    } forEach _units
};
["mirrorstance",
"Copy player's stance continuously. Unit mirrors player's posture until given a new stance or stop command. Triggers: copy my stance, mirror me, do as I do.",
"{}",
zdoArmaVoice_fnc_commandMirrorstance] call zdoArmaVoice_fnc_coreRegisterCommand
