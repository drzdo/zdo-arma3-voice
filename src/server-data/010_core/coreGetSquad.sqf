zdoArmaVoice_fnc_coreGetSquad = {
private _all = units group player;
private _result = [];
{
    if (_x != player) then {
        private _squadIndex = _all find _x;
        _result pushBack [_x call BIS_fnc_netId, name _x, str side _x, typeOf _x, rankId _x, getPosASL _x, assignedTeam _x, _squadIndex + 1];
    };
} forEach _all;
_result
}
