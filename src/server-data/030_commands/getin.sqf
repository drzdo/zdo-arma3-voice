zdoArmaVoice_fnc_commandGetin = {
    params ["_args", "_lookAtPosition", "_units"];
    private _nearby = nearestObjects [_lookAtPosition, ["Car", "Tank", "Helicopter", "Plane", "Ship_F"], 15] select { !(_x isKindOf "Man") };
    if (count _nearby == 0) exitWith { systemChat "No vehicle found" };
    private _veh = _nearby select 0;
    private _role = toLower (_args getOrDefault ["role", "cargo"]);
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        if (_u distance _veh < 7) then {
            if (vehicle _u == _veh) then { moveOut _u };
            switch (_role) do {
                case "driver": { _u moveInDriver _veh };
                case "gunner": { _u moveInGunner _veh };
                case "commander": { _u moveInCommander _veh };
                default { _u moveInCargo _veh };
            }
        } else {
            _u doMove (getPosATL _veh);
            systemChat format ["%1 moving to vehicle", name _u]
        }
    } forEach _units;
    [_units, "getin"] call zdoArmaVoice_fnc_buildAckInstruction
};
["getin",
"Get in a vehicle at crosshair. Optionally specify role. Triggers: get in, mount up, get in as driver.",
"{role?: driver/gunner/commander/cargo}",
zdoArmaVoice_fnc_commandGetin] call zdoArmaVoice_fnc_coreRegisterCommand
