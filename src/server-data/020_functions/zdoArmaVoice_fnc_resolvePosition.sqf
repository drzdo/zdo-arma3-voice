zdoArmaVoice_fnc_resolvePosition = {
    params ["_spec", "_lookAtPosition"];
    if (_spec isEqualType "") exitWith {
        if (_spec == "lookAt") then { _lookAtPosition } else { _lookAtPosition }
    };
    if (_spec isEqualType []) exitWith { _spec };
    private _type = _spec getOrDefault ["type", ""];
    switch (_type) do {
        case "relative": {
            private _distance = _spec getOrDefault ["distance", 100];
            private _direction = toLower (_spec getOrDefault ["direction", "forward"]);
            private _pos = getPosATL player;
            private _playerDir = getDir player;
            private _bearing = switch (_direction) do {
                case "forward"; case "front"; case "ahead": { _playerDir };
                case "back"; case "backward"; case "behind": { _playerDir + 180 };
                case "left": { _playerDir - 90 };
                case "right": { _playerDir + 90 };
                case "north": { 0 };
                case "south": { 180 };
                case "east": { 90 };
                case "west": { 270 };
                default { _playerDir };
            };
            private _rad = _bearing * pi / 180;
            [(_pos select 0) + _distance * sin _rad, (_pos select 1) + _distance * cos _rad, _pos select 2]
        };
        case "azimuth": {
            private _distance = _spec getOrDefault ["distance", 100];
            private _rad = (_spec getOrDefault ["bearing", 0]) * pi / 180;
            private _pos = getPosATL player;
            [(_pos select 0) + _distance * sin _rad, (_pos select 1) + _distance * cos _rad, _pos select 2]
        };
        case "marker": {
            getMarkerPos (_spec getOrDefault ["marker", ""])
        };
        case "named": {
            [_spec getOrDefault ["name", ""]] call zdoArmaVoice_fnc_getNamedPos
        };
        default { _lookAtPosition };
    }
}
