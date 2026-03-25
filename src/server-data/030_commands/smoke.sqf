zdoArmaVoice_fnc_commandSmoke = {
    params ["_args", "_lookAtPosition", "_units"];
    private _color = toLower (_args getOrDefault ["color", "white"]);
    private _wanted = switch (_color) do {
        case "red": { "SmokeShellRed" };
        case "green": { "SmokeShellGreen" };
        case "blue": { "SmokeShellBlue" };
        case "orange": { "SmokeShellOrange" };
        case "purple": { "SmokeShellPurple" };
        case "yellow": { "SmokeShellYellow" };
        default { "SmokeShell" };
    };
    private _allSmokes = ["SmokeShell","SmokeShellRed","SmokeShellGreen","SmokeShellBlue","SmokeShellOrange","SmokeShellPurple","SmokeShellYellow"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _mags = magazines _u;
        private _toThrow = "";
        if (_wanted in _mags) then {
            _toThrow = _wanted
        } else {
            { if (_x in _mags) exitWith { _toThrow = _x } } forEach _allSmokes
        };
        if (_toThrow != "") then {
            _u removeMagazine _toThrow;
            private _pos = getPosATL _u;
            private _dir = getDirVisual _u;
            private _grenade = _toThrow createVehicle [0,0,0];
            _grenade setPosATL [(_pos select 0) + 3 * sin _dir, (_pos select 1) + 3 * cos _dir, (_pos select 2) + 1.5];
            _grenade setVelocity [5 * sin _dir, 5 * cos _dir, 3]
        } else {
            systemChat format ["%1 has no smoke grenades", name _u]
        }
    } forEach _units;
    [_units, "smoke"] call zdoArmaVoice_fnc_buildAckInstruction
};
["smoke",
"Pop smoke grenade near units. Triggers: smoke, pop smoke, throw smoke, cover with smoke, дым, дымовую, кинь дымовую.",
"{color?: white/red/green/blue/orange/purple/yellow}",
zdoArmaVoice_fnc_commandSmoke] call zdoArmaVoice_fnc_coreRegisterCommand
