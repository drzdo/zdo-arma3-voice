zdoArmaVoice_fnc_commandHoldfire = {
    params ["_args", "_lookAtPosition", "_units"];
    { (_x call BIS_fnc_objectFromNetId) setUnitCombatMode "BLUE" } forEach _units
};
["holdfire",
"Order units to hold fire, do not engage. Triggers: hold fire, cease fire, не стрелять, прекратить огонь.",
"{}",
zdoArmaVoice_fnc_commandHoldfire] call zdoArmaVoice_fnc_coreRegisterCommand
