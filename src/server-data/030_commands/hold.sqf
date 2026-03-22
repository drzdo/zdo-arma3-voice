["hold",
"Stop and LOCK position. Units will not move until given new orders. Triggers: hold position.",
"{units: Units, stance?: DOWN/MIDDLE/UP/AUTO}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _stance = _args getOrDefault ["stance", ""];
    [_units] call zdoArmaVoice_fnc_holdPosition;
    if (_stance != "") then { [_units, _stance] call zdoArmaVoice_fnc_setStance };
    [_units, "hold"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
