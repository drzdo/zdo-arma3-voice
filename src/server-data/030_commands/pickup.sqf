zdoArmaVoice_fnc_commandPickup = {
    params ["_args", "_lookAtPosition", "_units"];
    private _itemName = _args getOrDefault ["item", ""];
    private _nearbyItems = _args getOrDefault ["nearbyItems", []];

    if (count _nearbyItems == 0) exitWith {
        createHashMapFromArray [
            ["retryWithContext", createHashMapFromArray [["includeItemsNearLookAt", true]]]
        ]
    };

    private _holders = nearestObjects [_lookAtPosition, ["WeaponHolderSimulated", "WeaponHolder", "GroundWeaponHolder", "Man"], 2];
    private _foundClass = "";
    private _foundHolder = objNull;
    private _foundType = "";
    {
        private _obj = _x;
        if (_foundClass == "") then {
            {
                private _class = _x;
                private _displayName = getText (configFile >> "CfgWeapons" >> _class >> "displayName");
                if (_displayName == _itemName) exitWith { _foundClass = _class; _foundHolder = _obj; _foundType = "weapon" }
            } forEach (weaponCargo _obj);
        };
        if (_foundClass == "") then {
            {
                private _class = _x;
                private _displayName = getText (configFile >> "CfgMagazines" >> _class >> "displayName");
                if (_displayName == _itemName) exitWith { _foundClass = _class; _foundHolder = _obj; _foundType = "magazine" }
            } forEach (magazineCargo _obj);
        };
        if (_foundClass == "") then {
            {
                private _class = _x;
                private _displayName = getText (configFile >> "CfgWeapons" >> _class >> "displayName");
                if (_displayName == "") then { _displayName = getText (configFile >> "CfgVehicles" >> _class >> "displayName") };
                if (_displayName == _itemName) exitWith { _foundClass = _class; _foundHolder = _obj; _foundType = "item" }
            } forEach (itemCargo _obj)
        }
    } forEach _holders;

    if (_foundClass == "") exitWith { systemChat format ["Item '%1' not found nearby", _itemName] };

    {
        private _u = _x call BIS_fnc_objectFromNetId;
        switch (_foundType) do {
            case "weapon": {
                private _muzzles = getArray (configFile >> "CfgWeapons" >> _foundClass >> "muzzles");
                private _compatMags = [];
                { _compatMags append getArray (configFile >> "CfgWeapons" >> _foundClass >> _x >> "magazines") } forEach _muzzles;
                removeAllWeapons _u;
                _u addWeapon _foundClass;
                { if (_x in _compatMags) then { _u addMagazine _x } } forEach (magazineCargo _foundHolder)
            };
            case "magazine": {
                _u addMagazine _foundClass
            };
            case "item": {
                _u addItem _foundClass
            };
        }
    } forEach _units;

    systemChat format ["Picked up: %1", _itemName];
    [_units, "pickup"] call zdoArmaVoice_fnc_buildAckInstruction
};
["pickup",
"Pick up an item near crosshair (weapon, pistol, grenade, ammo, equipment). Triggers: take that, pick up, grab the AK.",
"{item: display name of item to pick up (from nearby items list if available), nearbyItems?: array of available item names}",
zdoArmaVoice_fnc_commandPickup] call zdoArmaVoice_fnc_coreRegisterCommand
