zdoArmaVoice_fnc_resolveUnits = {
    params ["_refs"];
    if (count _refs == 0) exitWith { [] };
    private _result = [];
    {
        if (_x isEqualType "" && {_x == "all"}) then {
            { _result pushBack (_x call BIS_fnc_netId) } forEach ((units group player) - [player])
        } else {
            if (_x isEqualType "" && {_x == "last"}) then {
                _result = +zdoArmaVoice_lastAddressedUnits
            } else {
                if (_x isEqualType "" && {toUpper _x in ["RED","GREEN","BLUE","YELLOW","WHITE"]}) then {
                    _result append ([toUpper _x] call zdoArmaVoice_fnc_getTeamMembers)
                } else {
                    _result pushBack _x
                }
            }
        }
    } forEach _refs;
    _result = [_result] call zdoArmaVoice_fnc_filterAlive;
    if (count _result > 0) then { zdoArmaVoice_lastAddressedUnits = _result };
    _result
}
