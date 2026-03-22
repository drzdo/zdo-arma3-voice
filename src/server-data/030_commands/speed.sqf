["speed",
"Change movement speed. Triggers: sprint/fast (FULL), run (NORMAL), walk/slow (LIMITED).",
"{units: Units, speed: FULL/NORMAL/LIMITED}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _speed = _args getOrDefault ["speed", "NORMAL"];
    [_speed] call zdoArmaVoice_fnc_setSpeed
}] call zdoArmaVoice_fnc_coreRegisterCommand
