zdoArmaVoice_fnc_commandLoot = {
    params ["_args", "_lookAtPosition", "_units"];
    private _lootRadius = _args getOrDefault ["radius", 100];
    {
        private _unitNetId = _x;
        private _unit = _unitNetId call BIS_fnc_objectFromNetId;

        private _storage = objNull;
        if (vehicle _unit != _unit) then {
            _storage = vehicle _unit
        } else {
            private _nearby = nearestObjects [_lookAtPosition, ["Car", "Tank", "Helicopter", "Plane", "Ship_F", "ReammoBox_F"], 20] select { !(_x isKindOf "Man") };
            if (count _nearby > 0) then { _storage = _nearby select 0 }
        };

        [_unit, _lookAtPosition, _lootRadius, _storage] spawn {
            params ["_unit", "_center", "_radius", "_storage"];
            private _startTime = time;
            private _hasStorage = !isNull _storage;

            private _fnc_cancelled = {
                !alive _unit
                || { (_unit getVariable ["zdoArmaVoice_toldStopAt", 0]) > _startTime }
                || { (_unit getVariable ["zdoArmaVoice_toldRegroupAt", 0]) > _startTime }
                || { (_unit getVariable ["zdoArmaVoice_toldMoveAt", 0]) > _startTime }
            };

            private _fnc_moveAndWait = {
                params ["_target"];
                _unit doMove (getPosATL _target);
                private _timeout = time + 30;
                waitUntil { sleep 1; (call _fnc_cancelled) || (_unit distance _target < 4) || (time > _timeout) };
                !(call _fnc_cancelled) && (_unit distance _target < 4)
            };

            private _fnc_transferItem = {
                params ["_cls", "_source"];
                if (_hasStorage) then {
                    if (_cls isKindOf ["Rifle", configFile >> "CfgWeapons"]
                        || _cls isKindOf ["Launcher", configFile >> "CfgWeapons"]
                        || _cls isKindOf ["Pistol", configFile >> "CfgWeapons"]
                        || _cls isKindOf ["Binocular", configFile >> "CfgWeapons"]) then {
                        _storage addWeaponCargoGlobal [_cls, 1]
                    } else {
                        if (_cls isKindOf ["Default", configFile >> "CfgMagazines"]) then {
                            _storage addMagazineCargoGlobal [_cls, 1]
                        } else {
                            _storage addItemCargoGlobal [_cls, 1]
                        }
                    }
                } else {
                    if (_cls isKindOf ["Default", configFile >> "CfgMagazines"]) then {
                        _unit addMagazine _cls
                    } else {
                        _unit addItem _cls
                    }
                }
            };

            systemChat format ["%1 starting loot run%2", name _unit, if (_hasStorage) then { format [" (storage: %1)", typeOf _storage] } else { " (on self)" }];

            while {!(call _fnc_cancelled)} do {
                private _weaponPiles = _center nearObjects ["WeaponHolderSimulated", _radius];
                _weaponPiles append (_center nearObjects ["WeaponHolder", _radius]);
                _weaponPiles append (_center nearObjects ["GroundWeaponHolder", _radius]);

                private _bodies = entities "Man" select { !alive _x && _x distance _center < _radius };

                private _targets = _weaponPiles + _bodies;
                if (count _targets == 0) exitWith {};

                _targets = [_targets, [], {_x distance _unit}, "ASCEND"] call BIS_fnc_sortBy;

                private _looted = false;
                {
                    private _target = _x;
                    if (call _fnc_cancelled) exitWith {};

                    if (!([_target] call _fnc_moveAndWait)) then { continue };

                    if (_target isKindOf "Man") then {
                        {
                            _x params ["_cls", "_count"];
                            for "_i" from 1 to _count do {
                                [_cls, _target] call _fnc_transferItem;
                                _target removeMagazine _cls
                            }
                        } forEach (magazinesAmmoFull _target apply { [_x select 0, 1] });

                        { [_x, _target] call _fnc_transferItem } forEach (weapons _target);
                        { _target removeWeapon _x } forEach (weapons _target);

                        { [_x, _target] call _fnc_transferItem } forEach (items _target);
                        removeAllItems _target;

                        if (vest _target != "") then { [vest _target, _target] call _fnc_transferItem; removeVest _target };
                        if (headgear _target != "") then { [headgear _target, _target] call _fnc_transferItem; removeHeadgear _target };
                        if (backpack _target != "") then { [backpack _target, _target] call _fnc_transferItem; removeBackpack _target };
                    } else {
                        { [_x, _target] call _fnc_transferItem } forEach (weaponCargo _target);
                        { [_x, _target] call _fnc_transferItem } forEach (magazineCargo _target);
                        { [_x, _target] call _fnc_transferItem } forEach (itemCargo _target);
                        clearWeaponCargoGlobal _target;
                        clearMagazineCargoGlobal _target;
                        clearItemCargoGlobal _target;
                        deleteVehicle _target
                    };
                    _looted = true
                } forEach _targets;

                if (!_looted) exitWith {};
                sleep 1
            };

            if (call _fnc_cancelled) then {
                systemChat format ["%1 loot run cancelled", name _unit]
            } else {
                systemChat format ["%1 loot run complete", name _unit]
            }
        }
    } forEach _units
};
["loot",
"Loot an area — collect weapons, gear and items from weapon piles and dead bodies. Unit goes to each pile and collects. If unit is in a vehicle or vehicle is nearby, items go into the vehicle. Triggers: loot this area, go loot, collect gear.",
"{radius?: number (default 100)}",
zdoArmaVoice_fnc_commandLoot] call zdoArmaVoice_fnc_coreRegisterCommand
