zdoArmaVoice_fnc_commandOpenfire = {
    params ["_args", "_lookAtPosition", "_units"];
    if (count _units > 0) then {
        (group ((_units select 0) call BIS_fnc_objectFromNetId)) setCombatMode "RED"
    }
};
["openfire",
"Allow units to engage freely. Triggers: open fire, weapons free, fire at will, разрешить огонь, можно стрелять.",
"{}",
zdoArmaVoice_fnc_commandOpenfire] call zdoArmaVoice_fnc_coreRegisterCommand
