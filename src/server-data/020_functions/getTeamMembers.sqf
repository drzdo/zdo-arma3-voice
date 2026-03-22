zdoArmaVoice_fnc_getTeamMembers = {
params ["_team"];
private _armaTeam = if (_team == "WHITE") then { "MAIN" } else { _team };
(units group player - [player]) select { assignedTeam _x == _armaTeam } apply { _x call BIS_fnc_netId }
}
