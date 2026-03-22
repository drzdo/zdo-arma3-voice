["sitrep_health",
"Report health status via voice. Triggers: status report, how are you, report health.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _data = [_units] call zdoArmaVoice_fnc_getHealth;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["You are a soldier reporting health status to %1 %2. Be brief, natural, in character.", _pi select 1, _pi select 0]],
        ["message", format ["[HEALTH] Report your health. Data: %1", _data]]
    ]
}] call zdoArmaVoice_fnc_coreRegisterCommand
