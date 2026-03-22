zdoArmaVoice_fnc_commandGetin = {
    params ["_args", "_lookAtPosition", "_units"];
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _veh = nearestObject [_pos, "LandVehicle"];
    if (isNull _veh || _veh distance2D _pos >= 15) then { _veh = nearestObject [_pos, "Air"] };
    if (isNull _veh || _veh distance2D _pos >= 15) then { _veh = nearestObject [_pos, "Ship"] };
    if (isNull _veh || _veh distance2D _pos >= 15) exitWith { systemChat "No vehicle found" };
    private _role = toLower (_args getOrDefault ["role", "cargo"]);
    private _objs = _units apply { _x call BIS_fnc_objectFromNetId };
    {
        switch (_role) do {
            case "driver": { _x assignAsDriver _veh };
            case "gunner": { _x assignAsGunner _veh };
            case "commander": { _x assignAsCommander _veh };
            default { _x assignAsCargo _veh };
        }
    } forEach _objs;
    _objs orderGetIn true;
    [_units, "getin"] call zdoArmaVoice_fnc_buildAckInstruction
};
["getin",
"Get in a vehicle. Optionally specify role. Triggers: get in, mount up, get in as driver.",
"{position?: Position, role?: driver/gunner/commander/cargo}",
zdoArmaVoice_fnc_commandGetin] call zdoArmaVoice_fnc_coreRegisterCommand
