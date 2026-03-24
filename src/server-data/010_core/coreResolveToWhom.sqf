zdoArmaVoice_fnc_coreResolveToWhom = {
    params ["_refs"];
    if (count _refs == 0) exitWith { [] };
    private _result = [];
    private _allSquad = (units group player) - [player];
    {
        if (_x isEqualType 0) then {
            private _found = false;
            {
                private _s = str _x;
                private _colonIdx = _s find ":";
                if (_colonIdx >= 0) then {
                    private _num = parseNumber (_s select [_colonIdx + 1]);
                    if (_num == _x) exitWith {
                        _result pushBack (_x call BIS_fnc_netId);
                        _found = true
                    }
                }
            } forEach _allSquad;
            if (!_found) then {
                diag_log format ["ZdoArmaVoice: unit number %1 not found in squad", _x]
            }
        } else {
            if (_x isEqualType "" && {_x == "all"}) then {
                { _result pushBack (_x call BIS_fnc_netId) } forEach _allSquad
            } else {
                if (_x isEqualType "" && {_x == "last"}) then {
                    if (count zdoArmaVoice_lastAddressedUnits == 0) then {
                        zdoArmaVoice_lastAddressedUnits = _allSquad apply { _x call BIS_fnc_netId }
                    };
                    _result = +zdoArmaVoice_lastAddressedUnits
                } else {
                    if (_x isEqualType "" && {toUpper _x in ["RED","GREEN","BLUE","YELLOW","WHITE"]}) then {
                        _result append ([toUpper _x] call zdoArmaVoice_fnc_coreGetTeamMembers)
                    } else {
                        _result pushBack _x
                    }
                }
            }
        }
    } forEach _refs;
    _result = [_result] call zdoArmaVoice_fnc_coreFilterAlive;
    if (count _result > 0) then { zdoArmaVoice_lastAddressedUnits = _result };
    _result
}
