zdoArmaVoice_fnc_coreGetCommandSchemas = {
    private _result = createHashMap;
    {
        private _entry = zdoArmaVoice_registeredCommands get _x;
        _result set [_x, _entry get "schema"]
    } forEach (keys zdoArmaVoice_registeredCommands);
    _result
}
