zdoArmaVoice_fnc_commandSitrepHostiles = {
    params ["_args", "_lookAtPosition", "_units"];
    private _allTargets = [];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _uPos = getPosASL _u;
        private _uDir = getDirVisual _u;
        private _known = _u targets [true, 600];
        {
            private _target = _x;
            if (alive _target) then {
                private _tPos = getPosASL _target;
                private _dist = round (_uPos distance2D _tPos);
                private _dx = (_tPos select 0) - (_uPos select 0);
                private _dy = (_tPos select 1) - (_uPos select 1);
                private _bearing = round ((_dx atan2 _dy + 360) mod 360);
                private _relBearing = round ((_bearing - _uDir + 360) mod 360);
                private _typeName = getText (configFile >> "CfgVehicles" >> typeOf _target >> "displayName");
                if (_typeName == "") then { _typeName = typeOf _target };
                _allTargets pushBack [_typeName, str side _target, _dist, _bearing, _relBearing]
            }
        } forEach _known
    } forEach _units;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    private _unitName = name (_targetNetId call BIS_fnc_objectFromNetId);
    private _generic = call zdoArmaVoice_fnc_coreGenericSystemInstructionsPart;
    if (count _allTargets == 0) exitWith {
        createHashMapFromArray [
            ["type", "dialog"],
            ["targetNetId", _targetNetId],
            ["systemInstructions", format ["You are %1. Stay in character. Be brief. %2", _unitName, _generic]],
            ["message", "Report that you see no hostile contacts. One short sentence."]
        ]
    };
    _allTargets sort true;
    private _data = _allTargets select [0, count _allTargets min 8];
    private _msg = "Report hostile contacts. "
        + "STRICT: Use ONLY the data below. Do NOT invent or change any numbers. "
        + format ["Data [type, side, distance_m, absolute_bearing, relative_bearing_from_you]: %1. ", _data]
        + "For each contact say: type, distance, bearing number. "
        + "Convert relative_bearing to direction: 0=ahead, 90=right, 180=behind, 270=left. "
        + "1-3 sentences max.";
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["You are %1, reporting contacts to %2 %3. Stay in character. %4", _unitName, _pi select 1, _pi select 0, _generic]],
        ["message", _msg]
    ]
};
["sitrep_hostiles",
"Report known hostile contacts via voice. Triggers: report contacts, any hostiles, what do you see.",
"{}",
zdoArmaVoice_fnc_commandSitrepHostiles] call zdoArmaVoice_fnc_coreRegisterCommand
