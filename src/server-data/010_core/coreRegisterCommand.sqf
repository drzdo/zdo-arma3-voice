zdoArmaVoice_fnc_coreRegisterCommand = {
    params ["_id", "_description", "_schema", "_fn", ["_enableIf", {}]];
    zdoArmaVoice_registeredCommands set [_id, createHashMapFromArray [
        ["description", _description],
        ["schema", _schema],
        ["fn", _fn],
        ["enableIf", _enableIf]
    ]]
}
