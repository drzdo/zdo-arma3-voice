["attack",
"Engage/fire at a target. Triggers: attack, fire at, engage. Uses crosshair to find nearest enemy if no specific target named.",
"{units: Units, target?: netId string, position?: Position}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _targetId = _args getOrDefault ["target", ""];
    private _t = objNull;
    if (_targetId != "") then { _t = _targetId call BIS_fnc_objectFromNetId };
    if (isNull _t) then { _t = [_pos] call zdoArmaVoice_fnc_findEnemyAt };
    if (isNull _t) exitWith { systemChat "No target found" };
    { private _u = _x call BIS_fnc_objectFromNetId; _u reveal [_t, 4]; _u doFire _t } forEach _units;
    [_units, "attack"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
