["watch",
"Order units to watch/face a direction or position. Triggers: watch there, watch south. For cardinal direction use position with type relative and distance=100.",
"{units: Units, position?: Position}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    [_units, _pos] call zdoArmaVoice_fnc_watch
}] call zdoArmaVoice_fnc_coreRegisterCommand
