zdoArmaVoice_fnc_coreIntentPrompt = {
    params ["_playerText"];

    private _lookAtPos = call zdoArmaVoice_fnc_determineTargetPosition;

    private _squad = call zdoArmaVoice_fnc_getSquad;
    private _squadContext = "";
    {
        _x params ["_netId", "_name", "_side", "_type", "_rankId", "_pos", "_team", "_idx"];
        _squadContext = _squadContext + format ["  #%1 netId=%2 name=%3 type=%4", _idx, str _netId, str _name, _type] + toString [10]
    } forEach _squad;

    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _playerName = _pi select 0;
    private _playerRank = _pi select 1;

    private _markers = call zdoArmaVoice_fnc_getMarkers;
    private _markerContext = "";
    if (count _markers > 0) then {
        _markerContext = "  Map markers: " + str _markers + toString [10]
    };

    private _cmdList = "";
    private _keys = keys zdoArmaVoice_registeredCommands;
    _keys sort true;
    {
        private _entry = zdoArmaVoice_registeredCommands get _x;
        private _desc = _entry get "description";
        private _schema = _entry get "schema";
        _cmdList = _cmdList + format ["- %1: %2 Args: %3", str _x, _desc, _schema] + toString [10]
    } forEach _keys;

    private _nl = toString [10];
    private _system = "You are a military voice command parser for Arma 3." + _nl
        + "Parse the player's speech into a structured JSON command." + _nl
        + "The player may speak in any language (English, Russian, etc)." + _nl
        + _nl
        + "Mission context: NATO forces patrol. CSAT insurgents operate in the area." + _nl
        + _nl
        + "Known units:" + _nl
        + "  Player: " + _playerRank + " " + _playerName + _nl
        + "  Player's squad (in order):" + _nl
        + _squadContext
        + _markerContext
        + _nl
        + "=== COMMON ARG TYPES ===" + _nl
        + "Units: array of strings. Values: netIds from unit list, ""all"", team color (""red"",""green"",""blue"",""yellow""), or ""last"" (reuse previous)." + _nl
        + "Position: ""lookAt"" (crosshair/map cursor) | {""type"":""relative"",""distance"":N,""direction"":""forward""/""back""/""left""/""right""/""north""/""south""/""east""/""west""} | {""type"":""azimuth"",""distance"":N,""bearing"":0-360} | {""type"":""marker"",""marker"":""markerName""} | {""type"":""named"",""name"":""objectName""}." + _nl
        + "  If direction without distance (""go south""), use distance=100." + _nl
        + _nl
        + "=== AVAILABLE COMMANDS ===" + _nl
        + _cmdList
        + _nl
        + "=== RESPONSE FORMAT ===" + _nl
        + "Return a JSON array of commands to execute:" + _nl
        + "[{""command"": ""command_id"", ""args"": {... per command schema ...}}]" + _nl
        + _nl
        + "If the player gives TWO commands at once (e.g. ""move stealthily"" = move + behaviour)," + _nl
        + "return multiple objects in the array." + _nl
        + _nl
        + "=== RULES ===" + _nl
        + "- Return ONLY valid JSON. No markdown, no explanation." + _nl
        + "- Prefer returning netIds over names for unit references." + _nl
        + "- ""dialog"" is ONLY for conversation/questions, NEVER for giving orders." + _nl
        + "- Ignore filler words." + _nl
        + "- If the player does NOT mention who should execute, use ""units"": [""last""]." + _nl
        + "- Only default to [""all""] if it is the very first command or the player clearly means everyone." + _nl
        + "- Team numbers map to colors: 1=""red"", 2=""green"", 3=""blue"", 4=""yellow""." + _nl
        + "- If the player says ""second""/ordinal, use the squad # and return that unit's netId.";

    createHashMapFromArray [
        ["systemInstructions", _system],
        ["message", _playerText],
        ["lookAtPosition", _lookAtPos]
    ]
}
