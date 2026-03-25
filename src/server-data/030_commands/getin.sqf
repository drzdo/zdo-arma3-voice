zdoArmaVoice_fnc_commandGetin = {
    params ["_args", "_lookAtPosition", "_units"];
    private _nearby = nearestObjects [_lookAtPosition, ["Car", "Tank", "Helicopter", "Plane", "Ship_F"], 15] select { !(_x isKindOf "Man") };
    if (count _nearby == 0) exitWith { systemChat "No vehicle found" };
    private _veh = _nearby select 0;
    private _role = toLower (_args getOrDefault ["role", "cargo"]);
    {
        private _unitNetId = _x;
        [_unitNetId, _veh, _role] spawn {
            params ["_unitNetId", "_veh", "_role"];
            private _unit = _unitNetId call BIS_fnc_objectFromNetId;
            private _startTime = time;

            private _fnShouldStop = { [_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand || { isNull _veh } };

            if (call _fnShouldStop) exitWith {};

            if ((_unit distance _veh) >= 7) then {
                _unit doMove (getPosATL _veh);
                systemChat format ["%1 moving to vehicle", name _unit];
                private _timeout = time + 60;
                waitUntil {
                    sleep 1;
                    (call _fnShouldStop)
                    || { (_unit distance _veh) < 7 }
                    || { time > _timeout }
                };
            };

            if (call _fnShouldStop) exitWith {
                systemChat format ["%1 getting in was cancelled", name _unit]
            };
            if ((_unit distance _veh) >= 7) exitWith {
                systemChat format ["%1 couldn't reach vehicle", name _unit]
            };

            if (vehicle _unit == _veh) then { moveOut _unit };
            switch (_role) do {
                case "driver": { _unit moveInDriver _veh };
                case "gunner": { _unit moveInGunner _veh };
                case "commander": { _unit moveInCommander _veh };
                default { _unit moveInCargo _veh };
            }
        }
    } forEach _units;
    [_units, "getin"] call zdoArmaVoice_fnc_buildAckInstruction
};
["getin",
"Get in a vehicle at crosshair. Optionally specify role. Triggers: get in, mount up, get in as driver, садись, залезай, в машину.",
"{role?: driver/gunner/commander/cargo}",
zdoArmaVoice_fnc_commandGetin] call zdoArmaVoice_fnc_coreRegisterCommand
