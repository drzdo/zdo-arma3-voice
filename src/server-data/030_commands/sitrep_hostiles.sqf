zdoArmaVoice_fnc_commandSitrepHostiles = {
    params ["_args", "_lookAtPosition", "_units"];
    private _allTargets = [];
    private _seen = [];
    private _reporter = (_units select 0) call BIS_fnc_objectFromNetId;
    private _rPos = getPosASL _reporter;
    private _rDir = getDirVisual _reporter;
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _known = _u targets [true, 600];
        {
            private _target = _x;
            if (alive _target && {!(_target in _seen)}) then {
                _seen pushBack _target;
                private _tPos = getPosASL _target;
                private _dist = round (_rPos distance2D _tPos);
                private _dx = (_tPos select 0) - (_rPos select 0);
                private _dy = (_tPos select 1) - (_rPos select 1);
                private _bearing = round ((_dx atan2 _dy + 360) mod 360);
                private _relBearing = round ((_bearing - _rDir + 360) mod 360);
                private _typeName = getText (configFile >> "CfgVehicles" >> typeOf _target >> "displayName");
                if (_typeName == "") then { _typeName = typeOf _target };
                private _compassDir = switch (true) do {
                    case (_relBearing < 23): { "ahead" };
                    case (_relBearing < 68): { "front-right" };
                    case (_relBearing < 113): { "right" };
                    case (_relBearing < 158): { "rear-right" };
                    case (_relBearing < 203): { "behind" };
                    case (_relBearing < 248): { "rear-left" };
                    case (_relBearing < 293): { "left" };
                    case (_relBearing < 338): { "front-left" };
                    default { "ahead" };
                };
                private _roundDist = (round (_dist / 25)) * 25 max 25;
                _allTargets pushBack format ["%1, %2m, %3, azimuth %4", _typeName, _roundDist, _compassDir, _bearing]
            }
        } forEach _known
    } forEach _units;
    private _pi = call zdoArmaVoice_fnc_coreGetPlayerInfo;
    private _targetNetId = if (count _units > 0) then { _units select 0 } else { "" };
    private _unitName = name (_targetNetId call BIS_fnc_objectFromNetId);
    private _personality = [_targetNetId] call zdoArmaVoice_fnc_coreUnitPersonality;
    if (count _allTargets == 0) exitWith {
        createHashMapFromArray [
            ["type", "dialog"],
            ["targetNetId", _targetNetId],
            ["systemInstructions", format ["Be very brief. One short sentence. %1", _personality]],
            ["message", "Say you see no contacts. Do not introduce yourself."]
        ]
    };
    private _data = _allTargets select [0, count _allTargets min 8];
    private _nl = toString [10];
    private _report = _data joinString _nl;
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _targetNetId],
        ["systemInstructions", format ["Report contacts in terse military radio style. Just list contacts: type, distance, azimuth, direction. No greetings, no name, no filler. Example: 'Rifleman, 50 meters, azimuth 130, south-east. Two officers, 100 meters, azimuth 270, west.' %1", _personality]],
        ["message", "Hostiles:" + _nl + _report]
    ]
};
["sitrep_hostiles",
"Report known hostile contacts via voice. Triggers: report contacts, any hostiles, what do you see.",
"{}",
zdoArmaVoice_fnc_commandSitrepHostiles] call zdoArmaVoice_fnc_coreRegisterCommand
