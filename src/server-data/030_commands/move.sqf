["move",
"Move units to a position. Triggers: go there, move to that building, 100 meters forward, move stealthily, run there.",
"{units: Units, position: Position, stance?: DOWN/MIDDLE/UP/AUTO, speed?: FULL/NORMAL/LIMITED, formation?: STEALTH/AWARE/COMBAT/SAFE}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _stance = _args getOrDefault ["stance", ""];
    private _speed = _args getOrDefault ["speed", ""];
    private _formation = _args getOrDefault ["formation", ""];
    [_units, _pos] call zdoArmaVoice_fnc_moveUnits;
    if (_stance != "") then { [_units, _stance] call zdoArmaVoice_fnc_setStance };
    if (_speed != "") then { [_speed] call zdoArmaVoice_fnc_setSpeed };
    if (_formation != "") then { [_units, _formation] call zdoArmaVoice_fnc_setBehaviour };
    [_units, "move"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
