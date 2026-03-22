["stance",
"Change unit stance/posture. Triggers: stand up (UP), crouch (MIDDLE), prone (DOWN), auto (AUTO).",
"{units: Units, stance: DOWN/MIDDLE/UP/AUTO, speed?: FULL/NORMAL/LIMITED}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _stance = _args getOrDefault ["stance", "UP"];
    private _speed = _args getOrDefault ["speed", ""];
    [_units, _stance] call zdoArmaVoice_fnc_setStance;
    if (_speed != "") then { [_speed] call zdoArmaVoice_fnc_setSpeed };
    [_units, "stance"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
