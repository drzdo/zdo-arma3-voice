zdoArmaVoice_fnc_commandSwitchback = {
    params ["_args", "_lookAtPosition", "_units"];
    if (isNull zdoArmaVoice_originalUnit) exitWith {};
    private _current = player;
    private _original = zdoArmaVoice_originalUnit;
    selectPlayer _original;
    zdoArmaVoice_originalUnit = objNull;
    systemChat format ["Switched back to %1", name _original]
};
["switchback",
"Return to your original unit after switching. Triggers: that's enough, switch back, return to my body, go back.",
"{}",
zdoArmaVoice_fnc_commandSwitchback,
{ !isNull zdoArmaVoice_originalUnit }] call zdoArmaVoice_fnc_coreRegisterCommand
