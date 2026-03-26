zdoArmaVoice_fnc_commandOpenfire = {
    params ["_args", "_lookAtPosition", "_units"];
    { (_x call BIS_fnc_objectFromNetId) setUnitCombatMode "RED" } forEach _units
};
["openfire",
"Allow units to engage freely. Triggers: open fire, weapons free, fire at will, разрешить огонь, можно стрелять.",
"{}",
zdoArmaVoice_fnc_commandOpenfire] call zdoArmaVoice_fnc_coreRegisterCommand
