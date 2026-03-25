zdoArmaVoice_fnc_commandGetout = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        if (vehicle _u != _u) then { moveOut _u }
    } forEach _units
};
["getout",
"Get out of current vehicle. Triggers: get out, dismount, вылезай, из машины, выходи.",
"{}",
zdoArmaVoice_fnc_commandGetout] call zdoArmaVoice_fnc_coreRegisterCommand
