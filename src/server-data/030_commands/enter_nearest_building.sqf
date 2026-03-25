zdoArmaVoice_fnc_commandEnterNearestBuilding = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _building = nearestObject [getPosATL _u, "House"];
        if (!isNull _building) then {
            private _positions = _building buildingPos -1;
            if (count _positions > 0) then {
                _u doMove (_positions select (floor random count _positions))
            }
        }
    } forEach _units;
    [_units, "enter_nearest_building"] call zdoArmaVoice_fnc_buildAckInstruction
};
["enter_nearest_building",
"Enter the nearest building to each unit. Triggers: get inside, hide in building, take cover inside, заходи в здание, спрячься в доме, внутрь.",
"{}",
zdoArmaVoice_fnc_commandEnterNearestBuilding] call zdoArmaVoice_fnc_coreRegisterCommand
