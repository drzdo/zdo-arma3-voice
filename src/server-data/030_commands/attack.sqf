zdoArmaVoice_fnc_commandAttack = {
    params ["_args", "_lookAtPosition", "_units"];
    private _targetId = _args getOrDefault ["target", ""];
    private _t = objNull;
    if (_targetId != "") then { _t = _targetId call BIS_fnc_objectFromNetId };
    if (isNull _t) then {
        private _enemies = _lookAtPosition nearEntities ["Man", 50] select { alive _x && side _x != side player };
        if (count _enemies > 0) then { _t = _enemies select 0 } else {
            private _vehicles = _lookAtPosition nearEntities ["LandVehicle", 50] select { alive _x && side _x != side player };
            if (count _vehicles > 0) then { _t = _vehicles select 0 }
        }
    };
    if (isNull _t) exitWith { systemChat "No target found" };
    { private _u = _x call BIS_fnc_objectFromNetId; _u reveal [_t, 4]; _u doFire _t } forEach _units;
    [_units, "attack"] call zdoArmaVoice_fnc_buildAckInstruction
};
["attack",
"Engage/fire at a target. Triggers: attack, fire at, engage.",
"{target?: netId string}",
zdoArmaVoice_fnc_commandAttack] call zdoArmaVoice_fnc_coreRegisterCommand
