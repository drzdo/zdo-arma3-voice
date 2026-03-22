zdoArmaVoice_fnc_resolvePosition = {
    params ["_spec", "_lookAtPosition"];
    if (count _lookAtPosition < 3) then { _lookAtPosition = getPosATL player };
    if (isNil "_spec") exitWith { _lookAtPosition };
    if (_spec isEqualType "") exitWith { _lookAtPosition };
    if (_spec isEqualType [] && {count _spec >= 2}) exitWith { _spec };
    if !(_spec isEqualType createHashMap) exitWith { _lookAtPosition };
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
                case "north-north-east": { 22.5 };
                case "north-east": { 45 };
                case "east-north-east": { 67.5 };
                case "east": { 90 };
                case "east-south-east": { 112.5 };
                case "south-east": { 135 };
                case "south-south-east": { 157.5 };
                case "south": { 180 };
                case "south-south-west": { 202.5 };
                case "south-west": { 225 };
                case "west-south-west": { 247.5 };
                case "west": { 270 };
                case "west-north-west": { 292.5 };
                case "north-west": { 315 };
                case "north-north-west": { 337.5 };
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
