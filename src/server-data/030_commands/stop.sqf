zdoArmaVoice_fnc_commandStop = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        doStop _u;
        _u setVariable ["zdoArmaVoice_toldStopAt", time]
    } forEach _units;
    [_units, "stop"] call zdoArmaVoice_fnc_buildAckInstruction
};
["stop",
"Cancel current action, stay put but remain responsive to new orders. Triggers: stop, freeze, halt, hold position.",
"{}",
zdoArmaVoice_fnc_commandStop] call zdoArmaVoice_fnc_coreRegisterCommand
