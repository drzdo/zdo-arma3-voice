["regroup",
"Regroup on the player, come back. Triggers: regroup, come to me, fall back. If regroup quietly, also set behaviour to STEALTH.",
"{units: Units, formation?: STEALTH/AWARE/COMBAT/SAFE, speed?: FULL/NORMAL/LIMITED}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _formation = _args getOrDefault ["formation", ""];
    private _speed = _args getOrDefault ["speed", ""];
    [_units] call zdoArmaVoice_fnc_regroup;
    if (_formation != "") then { [_units, _formation] call zdoArmaVoice_fnc_setBehaviour };
    if (_speed != "") then { [_speed] call zdoArmaVoice_fnc_setSpeed };
    [_units, "regroup"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
