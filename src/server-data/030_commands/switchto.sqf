zdoArmaVoice_fnc_commandSwitchto = {
    params ["_args", "_lookAtPosition", "_units"];
    if (count _units == 0) exitWith { systemChat "No unit specified" };
    private _targetNetId = _units select 0;
    private _target = _targetNetId call BIS_fnc_objectFromNetId;
    if (isNull _target || !alive _target) exitWith { systemChat "Unit not found or dead" };
    zdoArmaVoice_originalUnit = player;
    doStop player;
    selectPlayer _target;
    systemChat format ["Switched to %1", name _target]
};
["switchto",
"Look through another unit's eyes / take control. Triggers: let me look from your eyes, switch to, take control of.",
"{}",
zdoArmaVoice_fnc_commandSwitchto,
{ isNull zdoArmaVoice_originalUnit }] call zdoArmaVoice_fnc_coreRegisterCommand
