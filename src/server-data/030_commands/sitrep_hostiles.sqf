["sitrep_hostiles",
"Report known hostile contacts via voice. Triggers: report contacts, any hostiles, what do you see.",
"{units: Units}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _data = [_units] call zdoArmaVoice_fnc_reportHostiles;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    private _msg = "[SITREP] Report hostile contacts. "
        + "STRICT: Use ONLY the data below. Do NOT invent or change any numbers. "
        + format ["Data [type, side, distance_m, absolute_bearing, relative_bearing_from_you]: %1. ", _data]
        + "For each contact say: type, distance, bearing number. "
        + "Convert relative_bearing to direction: 0=ahead, 90=right, 180=behind, 270=left. "
        + "If data is empty array [], say no contacts. 1-3 sentences max.";
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["You are a soldier reporting contacts to %1 %2. Stay in character.", _pi select 1, _pi select 0]],
        ["message", _msg]
    ]
}] call zdoArmaVoice_fnc_coreRegisterCommand
