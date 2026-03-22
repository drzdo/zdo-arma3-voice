zdoArmaVoice_fnc_commandBehaviour = {
    params ["_args", "_lookAtPosition", "_units"];
    private _mode = _args getOrDefault ["mode", "AWARE"];
    [_units, _mode] call zdoArmaVoice_fnc_setBehaviour;
    [_units, "behaviour"] call zdoArmaVoice_fnc_buildAckInstruction
};
["behaviour",
"Set unit behaviour/combat mode. Triggers: stealth, aware, combat, safe.",
"{mode: STEALTH/AWARE/COMBAT/SAFE}",
zdoArmaVoice_fnc_commandBehaviour] call zdoArmaVoice_fnc_coreRegisterCommand
