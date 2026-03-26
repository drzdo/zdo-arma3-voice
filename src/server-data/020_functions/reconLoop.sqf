zdoArmaVoice_fnc_reconLoop = {
    params ["_unitNetIds", "_destination", "_moveBearing", "_stance"];
    private _startTime = time;
    private _stepDistance = 30;
    private _scanTime = 8;
    private _scanSpread = 30;
    private _spacing = 5;
    private _moveTimeout = 45;

    private _units = _unitNetIds apply { _x call BIS_fnc_objectFromNetId };

    // --- Helpers ---
    private _fnUnitOk = {
        params ["_u"];
        alive _u && { !(_u getVariable ["ace_medical_isUnconscious", false]) }
    };
    private _fnActiveUnits = {
        _units select { [_x] call _fnUnitOk }
    };
    private _fnCancelled = {
        private _any = false;
        { if ((_x getVariable ["zdoArmaVoice_toldStopAt", 0]) > _startTime
            || (_x getVariable ["zdoArmaVoice_toldRegroupAt", 0]) > _startTime
            || (_x getVariable ["zdoArmaVoice_toldMoveAt", 0]) > _startTime) exitWith { _any = true }
        } forEach _units;
        _any
    };
    private _fnAnyHostiles = {
        private _spotter = objNull;
        { if (count (_x targets [true, 600]) > 0) exitWith { _spotter = _x } } forEach (call _fnActiveUnits);
        _spotter
    };
    private _fnShouldEnd = {
        (count (call _fnActiveUnits) == 0) || (call _fnCancelled)
    };
    private _fnReset = {
        { _x enableAI "AUTOTARGET" } forEach _units
    };

    // Set recon posture
    {
        _x setUnitPos _stance;
        _x setBehaviour "AWARE";
        _x disableAI "AUTOTARGET"
    } forEach _units;

    if (call _fnShouldEnd) exitWith { call _fnReset };

    // === Main loop: move 30m → wait → scan → repeat ===
    while { !(call _fnShouldEnd) } do {
        private _active = call _fnActiveUnits;
        if (count _active == 0) exitWith {};

        private _closest = _active select 0;
        { if ((_x distance2D _destination) < (_closest distance2D _destination)) then { _closest = _x } } forEach _active;
        if ((_closest distance2D _destination) < 10) exitWith {};

        private _leaderPos = getPosATL _closest;
        private _distLeft = _leaderPos distance2D _destination;
        private _stepDist = _stepDistance min _distLeft;
        private _nextCenter = [
            (_leaderPos select 0) + _stepDist * sin _moveBearing,
            (_leaderPos select 1) + _stepDist * cos _moveBearing,
            _leaderPos select 2
        ];

        // Move with formation spread
        private _perpBearing = _moveBearing + 90;
        private _activeCount = count _active;
        {
            private _offset = (_forEachIndex - (_activeCount - 1) / 2) * _spacing;
            private _unitWp = [
                (_nextCenter select 0) + _offset * sin _perpBearing,
                (_nextCenter select 1) + _offset * cos _perpBearing,
                _nextCenter select 2
            ];
            _x doMove _unitWp
        } forEach _active;

        // Wait for arrival or timeout
        private _deadline = time + _moveTimeout;
        private _arrived = [];
        waitUntil {
            sleep 1;
            if (call _fnShouldEnd) exitWith { true };
            if (!isNull (call _fnAnyHostiles)) exitWith { true };
            private _nowActive = call _fnActiveUnits;
            _arrived = _nowActive select { (_x distance2D _nextCenter) < 8 };
            (count _arrived >= count _nowActive) || { time > _deadline }
        };

        if (call _fnShouldEnd) exitWith {};
        if (!isNull (call _fnAnyHostiles)) exitWith {};

        // Drop stragglers
        private _active2 = call _fnActiveUnits;
        {
            if (!(_x in _arrived)) then {
                doStop _x;
                _x enableAI "AUTOTARGET";
                _units = _units - [_x];
                systemChat format ["%1: fell behind, leaving recon group", name _x]
            }
        } forEach _active2;

        if (count (call _fnActiveUnits) == 0) exitWith {};
        if (call _fnShouldEnd) exitWith {};

        // Scan forward (+/- random spread)
        private _scanBearing = _moveBearing + ((random (_scanSpread * 2)) - _scanSpread);
        {
            doStop _x;
            private _uPos = getPosATL _x;
            _x doWatch [
                (_uPos select 0) + 100 * sin _scanBearing,
                (_uPos select 1) + 100 * cos _scanBearing,
                _uPos select 2
            ]
        } forEach (call _fnActiveUnits);

        private _scanEnd = time + _scanTime;
        waitUntil {
            sleep 0.5;
            (call _fnShouldEnd) || { !isNull (call _fnAnyHostiles) } || { time > _scanEnd }
        };

        if (call _fnShouldEnd) exitWith {};
        if (!isNull (call _fnAnyHostiles)) exitWith {};
    };

    // === Post-loop ===
    if (call _fnCancelled) exitWith { call _fnReset };

    // Contact
    private _spotter = call _fnAnyHostiles;
    if (!isNull _spotter) exitWith {
        {
            _x setUnitPos "DOWN";
            _x setBehaviour "STEALTH"
        } forEach (call _fnActiveUnits);
        call _fnReset;

        private _targets = _spotter targets [true, 600];
        if (count _targets > 0) then {
            _spotter doWatch (getPosATL (_targets select 0))
        };

        private _spotterNetId = _spotter call BIS_fnc_netId;
        systemChat format ["%1: Contact!", name _spotter];
        "zdo_arma_voice" callExtension toJSON createHashMapFromArray [
            ["t", "exec"],
            ["command", "sitrep_hostiles"],
            ["args", createHashMap],
            ["units", [_spotterNetId]],
            ["lookAtPosition", [0,0,0]],
            ["isRadio", true]
        ]
    };

    // Clear
    call _fnReset;
    { doStop _x } forEach (call _fnActiveUnits);
    systemChat "Recon complete — area clear."
}
