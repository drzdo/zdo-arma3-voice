zdoArmaVoice_fnc_commandFormation = {
    params ["_args", "_lookAtPosition", "_units"];
    private _formation = _args getOrDefault ["formation", "COLUMN"];
    group player setFormation _formation
};
["formation",
"Change group formation. Triggers: wedge, line, column, diamond, клин, линия, колонна, ромб.",
"{formation: COLUMN/LINE/WEDGE/VEE/STAG COLUMN/DIAMOND/FILE/ECH LEFT/ECH RIGHT}",
zdoArmaVoice_fnc_commandFormation] call zdoArmaVoice_fnc_coreRegisterCommand
