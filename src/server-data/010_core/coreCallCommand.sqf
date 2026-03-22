zdoArmaVoice_fnc_coreCallCommand = {
    params ["_commandId", "_args", "_lookAtPosition", "_rawUnits"];
    private _units = [_rawUnits] call zdoArmaVoice_fnc_coreResolveToWhom;
    private _entry = zdoArmaVoice_registeredCommands getOrDefault [_commandId, createHashMap];
    private _fn = _entry getOrDefault ["fn", {}];
    [_args, _lookAtPosition, _units] call _fn
}
