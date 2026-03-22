["behaviour",
"Set unit behaviour/combat mode. Triggers: stealth, aware, combat, safe. If player just says one word without specifying units, use units=[all].",
"{units: Units, mode: STEALTH/AWARE/COMBAT/SAFE}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _mode = _args getOrDefault ["mode", "AWARE"];
    [_units, _mode] call zdoArmaVoice_fnc_setBehaviour;
    [_units, "behaviour"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
