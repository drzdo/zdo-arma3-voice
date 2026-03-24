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

            systemChat format ["%1 starting loot run%2", name _unit, if (_hasStorage) then { format [" (storage: %1)", typeOf _storage] } else { " (on self)" }];

            while {true} do {
                if ([_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand) exitWith {};

                private _weaponPiles = _center nearObjects ["WeaponHolderSimulated", _radius];
                _weaponPiles append (_center nearObjects ["WeaponHolder", _radius]);
                _weaponPiles append (_center nearObjects ["GroundWeaponHolder", _radius]);
                private _bodies = entities "Man" select { !alive _x && _x distance _center < _radius };
                private _targets = _weaponPiles + _bodies;

                if (count _targets == 0) exitWith {
                    systemChat format ["%1 loot run complete — nothing left", name _unit]
                };

                _targets = [_targets, [], {_x distance _unit}, "ASCEND"] call BIS_fnc_sortBy;

                private _didLoot = false;
                {
                    private _tgt = _x;
                    if ([_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand) exitWith {};

                    _unit doMove (getPos _tgt);
                    private _timeout = time + 30;
                    waitUntil {
                        sleep 1;
                        ([_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand)
                        || { _unit distance _tgt < 4 }
                        || { time > _timeout }
                    };

                    if ([_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand) exitWith {};
                    if (_unit distance _tgt >= 4) then { continue };

                    if (_tgt isKindOf "Man") then {
                        {
                            private _cls = _x select 0;
                            if (_hasStorage) then {
                                if (_cls isKindOf ["Default", configFile >> "CfgMagazines"]) then {
                                    _storage addMagazineCargoGlobal [_cls, 1]
                                } else {
                                    _storage addItemCargoGlobal [_cls, 1]
                                }
                            } else {
                                if (_cls isKindOf ["Default", configFile >> "CfgMagazines"]) then {
                                    _unit addMagazine _cls
                                } else {
                                    _unit addItem _cls
                                }
                            };
                            _tgt removeMagazine _cls
                        } forEach (magazinesAmmoFull _tgt apply { [_x select 0, 1] });

                        {
                            private _w = _x;
                            if (_hasStorage) then { _storage addWeaponCargoGlobal [_w, 1] } else { _unit addWeapon _w };
                            _tgt removeWeapon _w
                        } forEach (weapons _tgt);

                        { if (_hasStorage) then { _storage addItemCargoGlobal [_x, 1] } else { _unit addItem _x } } forEach (items _tgt);
                        removeAllItems _tgt;

                        if (vest _tgt != "") then { if (_hasStorage) then { _storage addItemCargoGlobal [vest _tgt, 1] }; removeVest _tgt };
                        if (headgear _tgt != "") then { if (_hasStorage) then { _storage addItemCargoGlobal [headgear _tgt, 1] }; removeHeadgear _tgt };
                        if (backpack _tgt != "") then { if (_hasStorage) then { _storage addBackpackCargoGlobal [backpack _tgt, 1] }; removeBackpack _tgt }
                    } else {
                        {
                            if (_hasStorage) then { _storage addWeaponCargoGlobal [_x, 1] } else { _unit addWeapon _x }
                        } forEach (weaponCargo _tgt);
                        {
                            if (_hasStorage) then { _storage addMagazineCargoGlobal [_x, 1] } else { _unit addMagazine _x }
                        } forEach (magazineCargo _tgt);
                        {
                            if (_hasStorage) then { _storage addItemCargoGlobal [_x, 1] } else { _unit addItem _x }
                        } forEach (itemCargo _tgt);
                        clearWeaponCargoGlobal _tgt;
                        clearMagazineCargoGlobal _tgt;
                        clearItemCargoGlobal _tgt;
                        deleteVehicle _tgt
                    };
                    _didLoot = true
                } forEach _targets;

                if (!_didLoot) exitWith {};
                sleep 1
            };

            if ([_unit, _startTime] call zdoArmaVoice_fnc_shouldStopCurrentCommand) then {
                systemChat format ["%1 loot run cancelled", name _unit]
            }
        }
    } forEach _units
};
["loot",
"Loot an area — collect weapons, gear and items from weapon piles and dead bodies. If unit is in a vehicle or vehicle is nearby, items go into the vehicle. Triggers: loot this area, go loot, collect gear.",
"{radius?: number (default 100)}",
zdoArmaVoice_fnc_commandLoot] call zdoArmaVoice_fnc_coreRegisterCommand
