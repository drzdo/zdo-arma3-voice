zdoArmaVoice_fnc_commandHoldfire = {
    params ["_args", "_lookAtPosition", "_units"];
    if (count _units > 0) then {
        (group ((_units select 0) call BIS_fnc_objectFromNetId)) setCombatMode "BLUE"
    }
};
["holdfire",
"Order units to hold fire, do not engage. Triggers: hold fire, cease fire, не стрелять, прекратить огонь.",
"{}",
zdoArmaVoice_fnc_commandHoldfire] call zdoArmaVoice_fnc_coreRegisterCommand
