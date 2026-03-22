zdoArmaVoice_fnc_commandSitrepPosition = {
    params ["_args", "_lookAtPosition", "_units"];
    private _text = _args getOrDefault ["text", ""];
    private _playerPos = getPosASL player;
    private _results = [];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _uPos = getPosASL _u;
        private _dist = _playerPos distance2D _uPos;
        private _roundedDist = (round (_dist / 25)) * 25 max 25;
        private _dx = (_uPos select 0) - (_playerPos select 0);
        private _dy = (_uPos select 1) - (_playerPos select 1);
        private _bearing = round (((_dx atan2 _dy + 360) mod 360) / 5) * 5;
        _results pushBack [name _u, _roundedDist, _bearing]
    } forEach _units;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["You are %1, a soldier reporting your position to %2 %3. Stay in character. Use rough estimates, not exact numbers. %4", name (_targetNetId call BIS_fnc_objectFromNetId), _pi select 1, _pi select 0, call zdoArmaVoice_fnc_coreGenericSystemInstructionsPart]],
        ["message", format ["[POSITION] The player asked: '%1'. Report your position. Data [name, approx_dist_m, approx_bearing]: %2. Use the approximate numbers as-is. Example: 'About 100 meters, bearing 215, south-west'. 1 sentence.", _text, _results]]
    ]
};
["sitrep_position",
"Report unit position relative to the player via voice. Triggers: where are you, report position, what is your azimuth.",
"{text?: what the player asked}",
zdoArmaVoice_fnc_commandSitrepPosition] call zdoArmaVoice_fnc_coreRegisterCommand
