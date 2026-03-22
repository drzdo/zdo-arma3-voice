["getin",
"Get in the vehicle the player is looking at. Optionally specify role: driver, gunner, commander, cargo (default). Triggers: get in, mount up, get in as driver.",
"{units: Units, position?: Position, role?: driver/gunner/commander/cargo}",
{
    params ["_args", "_lookAtPosition"];
    private _units = [_args getOrDefault ["units", ["all"]]] call zdoArmaVoice_fnc_resolveUnits;
    private _pos = [_args getOrDefault ["position", "lookAt"], _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;
    private _veh = [_pos] call zdoArmaVoice_fnc_findVehicleAt;
    if (isNull _veh) exitWith { systemChat "No vehicle found" };
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
    _objs commandGetIn _veh;
    [_units, "getin"] call zdoArmaVoice_fnc_buildAckInstruction
}] call zdoArmaVoice_fnc_coreRegisterCommand
