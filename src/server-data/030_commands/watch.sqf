zdoArmaVoice_fnc_commandWatch = {
    params ["_args", "_lookAtPosition", "_units"];
    private _posSpec = _args getOrDefault ["position", "lookAt"];
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        private _pos = if (_posSpec isEqualType createHashMap && {_posSpec getOrDefault ["type", ""] == "relative"}) then {
            private _distance = _posSpec getOrDefault ["distance", 100];
            private _direction = toLower (_posSpec getOrDefault ["direction", "forward"]);
            private _uPos = getPosATL _u;
            private _uDir = getDir _u;
            private _bearing = switch (_direction) do {
                case "forward"; case "front"; case "ahead": { _uDir };
                case "back"; case "backward"; case "behind": { _uDir + 180 };
                case "left": { _uDir - 90 };
                case "right": { _uDir + 90 };
                case "north": { 0 };
                case "north-east": { 45 };
                case "east": { 90 };
                case "south-east": { 135 };
                case "south": { 180 };
                case "south-west": { 225 };
                case "west": { 270 };
                case "north-west": { 315 };
                default { _uDir };
            };
            private _rad = _bearing * pi / 180;
            [(_uPos select 0) + _distance * sin _rad, (_uPos select 1) + _distance * cos _rad, _uPos select 2]
        } else {
            [_posSpec, _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition
        };
        _u doWatch _pos
    } forEach _units
};
["watch",
"Order units to watch/face a direction or position. Triggers: watch there, watch south. For cardinal directions use relative position with distance=100.",
"{position?: Position}",
zdoArmaVoice_fnc_commandWatch] call zdoArmaVoice_fnc_coreRegisterCommand
