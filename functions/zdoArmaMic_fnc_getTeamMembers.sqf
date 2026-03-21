params ["_team"];
units group player select { assignedTeam _x == _team } apply { _x call BIS_fnc_netId }
