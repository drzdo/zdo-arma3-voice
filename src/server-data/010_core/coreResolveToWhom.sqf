zdoArmaVoice_fnc_coreResolveToWhom = {
    params ["_refs"];
    if (count _refs == 0) exitWith { [] };
    private _result = [];
    private _allSquad = (units group player) - [player];
    {
        if (_x isEqualType 0) then {
            private _idx = _x - 1;
            if (_idx >= 0 && _idx < count _allSquad) then {
                _result pushBack ((_allSquad select _idx) call BIS_fnc_netId)
            } else {
                diag_log format ["ZdoArmaVoice: squad index %1 out of bounds (squad size %2)", _x, count _allSquad]
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
