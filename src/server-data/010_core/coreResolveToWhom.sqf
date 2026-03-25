zdoArmaVoice_fnc_coreResolveToWhom = {
    params ["_refs"];
    if (count _refs == 0) exitWith { [] };
    private _result = [];
    private _allSquad = (units group player) - [player];
    {
        private _ref = _x;
        if (_ref isEqualType 0) then {
            private _found = false;
            {
                private _s = str _x;
                private _colonIdx = _s find ":";
                if (_colonIdx >= 0) then {
                    private _num = parseNumber (_s select [_colonIdx + 1]);
                    if (_num == _ref) exitWith {
                        _result pushBack (_x call BIS_fnc_netId);
                        _found = true
                    }
                }
            } forEach _allSquad;
            if (!_found) then {
                diag_log format ["ZdoArmaVoice: unit number %1 not found in squad", _ref]
            }
        } else {
            if (_ref isEqualType "" && {_ref == "all"}) then {
                { _result pushBack (_x call BIS_fnc_netId) } forEach _allSquad
            } else {
                if (_ref isEqualType "" && {_ref == "last"}) then {
                    if (count zdoArmaVoice_lastAddressedUnits == 0) then {
                        zdoArmaVoice_lastAddressedUnits = _allSquad apply { _x call BIS_fnc_netId }
                    };
                    _result = +zdoArmaVoice_lastAddressedUnits
                } else {
                    if (_ref isEqualType "" && {toUpper _ref in ["RED","GREEN","BLUE","YELLOW","WHITE"]}) then {
                        _result append ([toUpper _ref] call zdoArmaVoice_fnc_coreGetTeamMembers)
                    } else {
                        _result pushBack _ref
                    }
                }
            }
        }
    } forEach _refs;
    _result = [_result] call zdoArmaVoice_fnc_coreFilterAlive;
    if (count _result > 0) then { zdoArmaVoice_lastAddressedUnits = _result };
    _result
}
