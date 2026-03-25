zdoArmaVoice_fnc_commandLookat = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    { (_x call BIS_fnc_objectFromNetId) doWatch _pos } forEach _units
};
["lookat",
"Make units look at a position or direction. Triggers: look there, look at that, смотри туда, смотри на восток. For cardinal directions use relative position with distance=100.",
"{position?: Position}",
zdoArmaVoice_fnc_commandLookat] call zdoArmaVoice_fnc_coreRegisterCommand
