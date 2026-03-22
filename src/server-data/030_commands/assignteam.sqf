zdoArmaVoice_fnc_commandAssignteam = {
    params ["_args", "_lookAtPosition", "_units"];
    private _team = toUpper (_args getOrDefault ["team", "RED"]);
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        _u assignTeam _team
    } forEach _units;
    systemChat format ["Assigned to team %1", _team];
    [_units, "assignteam"] call zdoArmaVoice_fnc_buildAckInstruction
};
["assignteam",
"Assign units to a team color. Triggers: assign to red team, join team alpha, в группу А.",
"{team: RED/GREEN/BLUE/YELLOW}",
zdoArmaVoice_fnc_commandAssignteam] call zdoArmaVoice_fnc_coreRegisterCommand
