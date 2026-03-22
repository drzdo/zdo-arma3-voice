zdoArmaVoice_fnc_coreCallCommand = {
    params ["_commandId", "_args", "_lookAtPosition"];
    private _entry = zdoArmaVoice_registeredCommands getOrDefault [_commandId, createHashMap];
    private _fn = _entry getOrDefault ["fn", {}];
    [_args, _lookAtPosition] call _fn
}
