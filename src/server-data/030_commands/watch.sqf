zdoArmaVoice_fnc_commandWatch = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    { (_x call BIS_fnc_objectFromNetId) doWatch _pos } forEach _units
};
["watch",
"Order units to watch/face a direction or position. Triggers: watch there, watch south, смотри туда, наблюдай на юг. For cardinal directions use relative position with distance=100.",
"{position?: Position}",
zdoArmaVoice_fnc_commandWatch] call zdoArmaVoice_fnc_coreRegisterCommand
