zdoArmaVoice_fnc_buildAckInstruction = {
    params ["_unitNetIds", "_commandName"];
    private _unit = (_unitNetIds select 0) call BIS_fnc_objectFromNetId;
    private _unitName = name _unit;
    private _unitRole = typeOf _unit;
    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _playerName = _pi select 0;
    private _playerRank = _pi select 1;
    createHashMapFromArray [
        ["ackSystemInstructions", format ["You are %1 (%2), a military NPC in Arma 3. The player (%3 %4) gave you a '%5' command. Give a very short military acknowledgment, 1 sentence max. Stay in character. Be brief.", _unitName, _unitRole, _playerRank, _playerName, _commandName]],
        ["ackMessage", format ["%1 acknowledges %2 command.", _unitName, _commandName]]
    ]
}
