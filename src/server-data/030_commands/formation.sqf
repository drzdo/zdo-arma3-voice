["formation",
"Change group formation. Triggers: wedge, line, column, diamond, etc.",
"{units: Units, formation: COLUMN/LINE/WEDGE/VEE/STAG COLUMN/DIAMOND/FILE/ECH LEFT/ECH RIGHT}",
{
    params ["_args", "_lookAtPosition"];
    private _formation = _args getOrDefault ["formation", "COLUMN"];
    [_formation] call zdoArmaVoice_fnc_setFormation
}] call zdoArmaVoice_fnc_coreRegisterCommand
