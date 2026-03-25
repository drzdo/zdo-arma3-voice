zdoArmaVoice_fnc_commandRecon = {
    params ["_args", "_lookAtPosition", "_units"];

    // Resolve destination — default: 100m forward from player
    private _distance = _args getOrDefault ["distance", 100];
    private _posSpec = _args getOrDefault ["position", createHashMapFromArray [["type", "relative"], ["direction", "forward"], ["distance", _distance]]];
    if (_posSpec isEqualType "lookAt") then {
        _posSpec = _lookAtPosition
    };
    private _destination = [_posSpec, _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;

    // Bearing from player to destination
    private _playerPos = getPosATL player;
    private _dx = (_destination select 0) - (_playerPos select 0);
    private _dy = (_destination select 1) - (_playerPos select 1);
    private _moveBearing = _dx atan2 _dy;

    // Stance: "prone" or "crouch" (default crouch)
    private _stanceArg = toLower (_args getOrDefault ["stance", "crouch"]);
    private _stance = if (_stanceArg in ["prone", "down"]) then { "DOWN" } else { "MIDDLE" };

    // Single spawn for the whole scout group
    [_units, _destination, _moveBearing, _stance] spawn {
        params ["_unitNetIds", "_destination", "_moveBearing", "_stance"];
        private _startTime = time;
        private _stepDistance = 30;
        private _scanTime = 8;
        private _scanSpread = 30; // random +/- degrees when scanning
        private _spacing = 5;
        private _moveTimeout = 45; // seconds to reach a 30m waypoint before unit is dropped

        // Resolve unit objects from netIds
        private _units = _unitNetIds apply { _x call BIS_fnc_objectFromNetId };

        // --- Helper: check if a unit is still capable ---
        private _fnUnitOk = {
            params ["_u"];
            alive _u && { !(_u getVariable ["ace_medical_isUnconscious", false]) }
        };

        // --- Helper: get capable units from the group ---
        private _fnActiveUnits = {
            _units select { [_x] call _fnUnitOk }
        };

        // --- Helper: cancelled by player (stop/regroup/move) ---
        private _fnCancelled = {
            private _any = false;
            { if ((_x getVariable ["zdoArmaVoice_toldStopAt", 0]) > _startTime
                || (_x getVariable ["zdoArmaVoice_toldRegroupAt", 0]) > _startTime
                || (_x getVariable ["zdoArmaVoice_toldMoveAt", 0]) > _startTime) exitWith { _any = true }
            } forEach _units;
            _any
        };

        // --- Helper: any active unit sees hostiles ---
        private _fnAnyHostiles = {
            private _spotter = objNull;
            { if (count (_x targets [true, 600]) > 0) exitWith { _spotter = _x } } forEach (call _fnActiveUnits);
            _spotter
        };

        // --- Helper: should the recon end ---
        private _fnShouldEnd = {
            (count (call _fnActiveUnits) == 0) || (call _fnCancelled)
        };

        // --- Helper: reset all units to normal AI state ---
        private _fnReset = {
            { _x enableAI "AUTOTARGET" } forEach _units
        };

        // Set recon posture for all units
        {
            _x setUnitPos _stance;
            _x setBehaviour "AWARE";
            _x disableAI "AUTOTARGET"
        } forEach _units;

        if (call _fnShouldEnd) exitWith { call _fnReset };

        // === Main recon loop ===
        // Each iteration: move group 30m → wait for all → scan forward ~8s → repeat
        while { !(call _fnShouldEnd) } do {
            private _active = call _fnActiveUnits;
            if (count _active == 0) exitWith {};

            // Check if destination reached (any active unit close enough)
            private _closest = _active select 0;
            { if (_x distance2D _destination < _closest distance2D _destination) then { _closest = _x } } forEach _active;
            if (_closest distance2D _destination < 10) exitWith {};

            // Calculate next waypoint along bearing
            private _leaderPos = getPosATL _closest;
            private _distLeft = _leaderPos distance2D _destination;
            private _stepDist = _stepDistance min _distLeft;
            private _nextCenter = [
                (_leaderPos select 0) + _stepDist * sin _moveBearing,
                (_leaderPos select 1) + _stepDist * cos _moveBearing,
                _leaderPos select 2
            ];

            // Stage A: move all active units to next waypoint (with formation spread)
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

            // Wait until all active units reach waypoint or timeout
            // Units that time out are dropped from the group
            private _deadline = time + _moveTimeout;
            private _arrived = [];
            waitUntil {
                sleep 1;
                if (call _fnShouldEnd) exitWith { true };
                if (!isNull (call _fnAnyHostiles)) exitWith { true };

                private _nowActive = call _fnActiveUnits;
                _arrived = _nowActive select { _x distance2D _nextCenter < 8 };
                // All active arrived, or timeout
                (count _arrived >= count _nowActive) || { time > _deadline }
            };

            if (call _fnShouldEnd) exitWith {};
            if (!isNull (call _fnAnyHostiles)) exitWith {};

            // Drop units that didn't make it in time
            private _active2 = call _fnActiveUnits;
            {
                if !(_x in _arrived) then {
                    doStop _x;
                    _x enableAI "AUTOTARGET";
                    _units = _units - [_x];
                    systemChat format ["%1: fell behind, leaving recon group", name _x]
                }
            } forEach _active2;

            if (count (call _fnActiveUnits) == 0) exitWith {};
            if (call _fnShouldEnd) exitWith {};

            // Stage B: stop and scan forward (+/- random spread) for ~8s
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

        // === Post-loop: contact or clear ===

        if (call _fnCancelled) exitWith { call _fnReset };

        // Contact: all go prone + stealth, spotter reports via radio
        // enableAI AFTER setting stealth so units don't auto-fire in the gap
        private _spotter = call _fnAnyHostiles;
        if (!isNull _spotter) exitWith {
            {
                _x setUnitPos "DOWN";
                _x setBehaviour "STEALTH"
            } forEach (call _fnActiveUnits);
            call _fnReset;

            // Spotter watches the first hostile
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

        // Destination reached, no contacts
        call _fnReset;
        { doStop _x } forEach (call _fnActiveUnits);
        systemChat "Recon complete — area clear."
    };
    [_units, "recon"] call zdoArmaVoice_fnc_buildAckInstruction
};
["recon",
"Move and recon — units advance in bounds as a group, scanning at each stop. If hostiles spotted, all go prone/stealth, spotter reports contacts. Triggers: recon forward, scout ahead, advance and recon.",
"{position?: Position, distance?: number (default 100), stance?: crouch/prone (default crouch)}",
zdoArmaVoice_fnc_commandRecon] call zdoArmaVoice_fnc_coreRegisterCommand
