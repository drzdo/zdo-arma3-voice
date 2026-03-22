["sitrep_position",
"Report unit position relative to the player via voice. Triggers: where are you, report position, what is your azimuth.",
"{units: Units, text?: what the player asked}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _text = _args getOrDefault ["text", ""];
    private _data = [_units] call zdoArmaVoice_fnc_getUnitPosition;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["You are a soldier reporting your position to %1 %2. Stay in character.", _pi select 1, _pi select 0]],
        ["message", format ["[POSITION] The player asked: '%1'. Report your position. Data [name,dist_m,bearing_degrees]: %2. ALWAYS include the numeric bearing/azimuth. Example: '120 meters, bearing 215, south-west'. 1 sentence.", _text, _data]]
    ]
}] call zdoArmaVoice_fnc_coreRegisterCommand
