zdoArmaVoice_fnc_commandDialog = {
    params ["_args", "_lookAtPosition", "_units"];
    private _target = _args getOrDefault ["target", ""];
    private _text = _args getOrDefault ["text", ""];
    private _unit = _target call BIS_fnc_objectFromNetId;
    private _unitName = if (!isNull _unit) then { name _unit } else { "Soldier" };
    private _unitRole = if (!isNull _unit) then { typeOf _unit } else { "Infantry" };
    private _unitSide = if (!isNull _unit) then { str side _unit } else { "UNKNOWN" };
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    createHashMapFromArray [
        ["type", "dialog"],
        ["targetNetId", _target],
        ["systemInstructions", format [
            "You are %1 (%2), a military NPC in Arma 3 on side %3. The player commanding you is %4 %5. Respond naturally as this character would in a military setting. Keep responses concise (1-3 sentences). Use appropriate military terminology. Do not break character. Do not use quotation marks around your own speech. %6",
            _unitName, _unitRole, _unitSide, _pi select 1, _pi select 0, [_target] call zdoArmaVoice_fnc_coreUnitPersonality
        ]],
        ["message", _text]
    ]
};
["dialog",
"Talk to an NPC conversationally (ask questions, make remarks). ONLY for actual conversation, NOT for giving orders.",
"{target: netId string of NPC to talk to, text: what the player said}",
zdoArmaVoice_fnc_commandDialog] call zdoArmaVoice_fnc_coreRegisterCommand
