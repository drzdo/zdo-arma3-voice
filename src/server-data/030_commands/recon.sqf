zdoArmaVoice_fnc_commandRecon = {
    params ["_args", "_lookAtPosition", "_units"];

    private _distance = _args getOrDefault ["distance", 100];
    private _posSpec = _args getOrDefault ["position", createHashMapFromArray [["type", "relative"], ["direction", "forward"], ["distance", _distance]]];
    if (_posSpec isEqualTo "lookAt") then {
        _posSpec = _lookAtPosition
    };
    private _destination = [_posSpec, _lookAtPosition] call zdoArmaVoice_fnc_resolvePosition;

    private _playerPos = getPosATL player;
    private _dx = (_destination select 0) - (_playerPos select 0);
    private _dy = (_destination select 1) - (_playerPos select 1);
    private _moveBearing = _dx atan2 _dy;

    private _stanceArg = toLower (_args getOrDefault ["stance", "crouch"]);
    private _stance = if (_stanceArg in ["prone", "down"]) then { "DOWN" } else { "MIDDLE" };

    [_units, _destination, _moveBearing, _stance] spawn zdoArmaVoice_fnc_reconLoop;
    [_units, "recon"] call zdoArmaVoice_fnc_buildAckInstruction
};
["recon",
"Move and recon — units advance in bounds as a group, scanning at each stop. If hostiles spotted, all go prone/stealth, spotter reports contacts. Triggers: recon forward, scout ahead, advance and recon, разведай, вперёд и разведай.",
"{position?: Position, distance?: number (default 100), stance?: crouch/prone (default crouch)}",
zdoArmaVoice_fnc_commandRecon] call zdoArmaVoice_fnc_coreRegisterCommand
