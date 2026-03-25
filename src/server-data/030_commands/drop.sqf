zdoArmaVoice_fnc_commandDrop = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        _u setUnitPos "DOWN";
        _u setVariable ["zdoArmaVoice_toldStanceAt", time]
    } forEach _units
};
["drop",
"Go prone immediately. Triggers: hit the dirt, get down, ложись, на землю, вниз.",
"{}",
zdoArmaVoice_fnc_commandDrop] call zdoArmaVoice_fnc_coreRegisterCommand
