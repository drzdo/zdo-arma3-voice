zdoArmaVoice_fnc_commandSpeed = {
    params ["_args", "_lookAtPosition", "_units"];
    private _speed = _args getOrDefault ["speed", "NORMAL"];
    [_speed] call zdoArmaVoice_fnc_setSpeed
};
["speed",
"Change movement speed. Triggers: sprint/fast (FULL), run (NORMAL), walk/slow (LIMITED), бегом (FULL), шагом (LIMITED), быстрее, медленнее.",
"{speed: FULL/NORMAL/LIMITED}",
zdoArmaVoice_fnc_commandSpeed] call zdoArmaVoice_fnc_coreRegisterCommand
