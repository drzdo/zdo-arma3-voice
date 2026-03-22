zdoArmaVoice_fnc_commandUnassignteam = {
    params ["_args", "_lookAtPosition", "_units"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        _u assignTeam "MAIN"
    } forEach _units;
    systemChat "Unassigned from team"
};
["unassignteam",
"Remove units from their team. Triggers: unassign from team, remove from team, leave team, из группы.",
"{}",
zdoArmaVoice_fnc_commandUnassignteam] call zdoArmaVoice_fnc_coreRegisterCommand
