zdoArmaVoice_fnc_coreIntentPrompt = {
    params ["_playerText", ["_isRadio", true], ["_extraContext", createHashMap]];

    private _lookAtPos = call zdoArmaVoice_fnc_determineTargetPosition;
    private _playerPos = getPosATL player;

    private _squad = call zdoArmaVoice_fnc_getSquad;
    private _filteredSquad = if (_isRadio) then {
        _squad
    } else {
        _squad select {
            _x params ["_netId", "_name", "_side", "_type", "_rankId", "_pos"];
            (_pos distance2D _playerPos) < 20
        }
    };

    private _squadContext = "";
    private _mode = if (_isRadio) then { "Radio" } else { "Direct (nearby units only)" };
    {
        _x params ["_netId", "_name", "_side", "_type", "_rankId", "_pos", "_team", "_idx"];
        _squadContext = _squadContext + format ["  #%1 netId=%2 name=%3 type=%4 team=%5", _idx, str _netId, str _name, _type, _team] + toString [10]
    } forEach _filteredSquad;

    private _pi = call zdoArmaVoice_fnc_getPlayerInfo;
    private _playerName = _pi select 0;
    private _playerRank = _pi select 1;

    private _markers = call zdoArmaVoice_fnc_getMarkers;
    private _markerContext = "";
    if (count _markers > 0) then {
        _markerContext = "  Map markers: " + str _markers + toString [10]
    };

    private _itemsContext = "";
    if (_extraContext getOrDefault ["includeItemsNearLookAt", false]) then {
        private _holders = nearestObjects [_lookAtPos, ["WeaponHolderSimulated", "WeaponHolder", "GroundWeaponHolder", "Man"], 2];
        private _items = [];
        {
            private _obj = _x;
            {
                private _displayName = getText (configFile >> "CfgWeapons" >> _x >> "displayName");
                if (_displayName != "") then { _items pushBack _displayName }
            } forEach (weaponCargo _obj + handgunItems _obj);
            {
                private _displayName = getText (configFile >> "CfgMagazines" >> _x >> "displayName");
                if (_displayName != "") then { _items pushBack _displayName }
            } forEach (magazineCargo _obj);
            {
                private _displayName = getText (configFile >> "CfgWeapons" >> _x >> "displayName");
                if (_displayName == "") then { _displayName = getText (configFile >> "CfgVehicles" >> _x >> "displayName") };
                if (_displayName != "") then { _items pushBack _displayName }
            } forEach (itemCargo _obj)
        } forEach _holders;
        if (count _items > 0) then {
            _itemsContext = "  Items near crosshair: " + str _items + toString [10]
        }
    };

    private _cmdList = "";
    private _keys = keys zdoArmaVoice_registeredCommands;
    _keys sort true;
    {
        private _entry = zdoArmaVoice_registeredCommands get _x;
        private _enableIf = _entry getOrDefault ["enableIf", {}];
        if (!isNil "_enableIf" && {!(_enableIf isEqualTo {})} && {!(call _enableIf)}) then { continue };
        private _desc = _entry get "description";
        private _schema = _entry get "schema";
        _cmdList = _cmdList + format ["- %1: %2 Args: %3", str _x, _desc, _schema] + toString [10]
    } forEach _keys;

    private _nl = toString [10];
    private _system = "You are a military voice command parser for Arma 3." + _nl
        + "The player speaks commands to control their squad. You parse speech into structured JSON." + _nl
        + "The player may speak in any language (English, Russian, Ukrainian, etc)." + _nl
        + "Ignore filler words." + _nl
        + _nl
        + "Mission context: NATO forces patrol. CSAT insurgents operate in the area." + _nl
        + _nl
        + "=== PLAYER'S SQUAD (" + _mode + ") ===" + _nl
        + "Player (#0): " + _playerRank + " " + _playerName + _nl
        + _squadContext
        + _markerContext
        + _itemsContext
        + _nl
        + "=== YOUR TASK ===" + _nl
        + "Determine three things from the player's speech:" + _nl
        + _nl
        + "1. TO WHOM — who is the player addressing?" + _nl
        + "   Return an array. Values can be mixed:" + _nl
        + "   - netId string: specific unit from the squad list (e.g. ""2:345"")" + _nl
        + "   - number: squad index (e.g. 2 = second unit, ""second""/""второй"" = 2)" + _nl
        + "     Player is #0, first squad member is #1, second is #2, etc." + _nl
        + "   - team color string: ""red"", ""green"", ""blue"", ""yellow""" + _nl
        + "   - ""all"": whole squad" + _nl
        + "   - ""last"": not specified, reuse previously addressed units" + _nl
        + "   Only use ""all"" if it is the very first command or the player clearly means everyone." + _nl
        + _nl
        + "2. WHAT — what command(s) does the player want to execute?" + _nl
        + "   Pick from the available commands below. If the player gives two commands at once" + _nl
        + "   (e.g. ""move stealthily"" = move + behaviour), return both." + _nl
        + _nl
        + "3. HOW — command-specific arguments per each command's schema" + _nl
        + _nl
        + "=== AVAILABLE COMMANDS ===" + _nl
        + _cmdList
        + _nl
        + "=== COMMON ARG TYPES ===" + _nl
        + "Position: ""lookAt"" (crosshair/map cursor) | {""type"":""relative"",""distance"":N,""direction"":DIR} | {""type"":""azimuth"",""distance"":N,""bearing"":0-360} | {""type"":""marker"",""marker"":""markerName""} | {""type"":""named"",""name"":""objectName""}." + _nl
        + "  DIR: forward/back/left/right/north/south/east/west/north-east/north-west/south-east/south-west/north-north-east/east-north-east/east-south-east/south-south-east/south-south-west/west-south-west/west-north-west/north-north-west" + _nl
        + "  If direction without distance (""go south""), use distance=100." + _nl
        + _nl
        + "=== RESPONSE FORMAT ===" + _nl
        + "Return JSON:" + _nl
        + "{" + _nl
        + "  ""units"": [""netId"" | ""all"" | ""red"" | ""last"" | 2 | ...],  // TO WHOM" + _nl
        + "  ""commands"": [  // WHAT + HOW" + _nl
        + "    {""command"": ""command_id"", ""args"": {... per schema ...}}" + _nl
        + "  ]" + _nl
        + "}" + _nl
        + _nl
        + "=== EXAMPLES ===" + _nl
        + "Speech: ""run to me!"" ->" + _nl
        + "{""units"":[""all""],""commands"":[{""command"":""regroup"",""args"":{}},{""command"":""stance"",""args"":{""stance"":""UP"",""speed"":""FULL""}}]}" + _nl
        + _nl
        + "Speech: ""crawl to me"" ->" + _nl
        + "{""units"":[""all""],""commands"":[{""command"":""regroup"",""args"":{}},{""command"":""stance"",""args"":{""stance"":""DOWN""}}]}" + _nl
        + _nl
        + "Speech: ""quiet!"" ->" + _nl
        + "{""units"":[""all""],""commands"":[{""command"":""behaviour"",""args"":{""mode"":""STEALTH""}},{""command"":""drop"",""args"":{}}]}" + _nl
        + _nl
        + "=== RULES ===" + _nl
        + "- Return ONLY valid JSON. No markdown, no explanation." + _nl
        + "- Prefer netIds over names." + _nl
        + "- ""dialog"" is ONLY for conversation/questions, NEVER for giving orders." + _nl
        + "- Command args should NOT contain ""units"" — units are top-level.";

    createHashMapFromArray [
        ["systemInstructions", _system],
        ["message", _playerText],
        ["lookAtPosition", _lookAtPos],
        ["isRadio", _isRadio]
    ]
}
