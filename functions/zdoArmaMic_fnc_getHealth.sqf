params ["_netIds"];
private _reports = [];
{
    private _u = _x call BIS_fnc_objectFromNetId;
    private _name = name _u;
    private _report = "";

    if (!alive _u) then {
        _report = _name + " is dead.";
    } else {
        if (isClass (configFile >> "CfgPatches" >> "ace_medical")) then {
            /* ACE3 medical */
            private _wounds = _u getVariable ["ace_medical_openWounds", []];
            private _pain = _u getVariable ["ace_medical_pain", 0];
            private _bleeding = _u getVariable ["ace_medical_isBleeding", false];
            private _unconscious = _u getVariable ["ace_medical_isUnconscious", false];
            private _parts = [];

            if (_unconscious) then { _parts pushBack "unconscious" };
            if (_bleeding) then { _parts pushBack "bleeding" };
            if (_pain > 0.3) then { _parts pushBack format ["in pain (%.0f%%)", _pain * 100] };

            {
                _x params ["_bodyPart", "_classID", "_amount"];
                private _partName = ["head","body","left arm","right arm","left leg","right leg"] select _bodyPart;
                if (_amount > 0) then {
                    _parts pushBack format ["%1 wound on %2", _amount, _partName];
                };
            } forEach _wounds;

            if (count _parts == 0) then {
                _report = _name + " is fine.";
            } else {
                _report = _name + ": " + (_parts joinString ", ") + ".";
            };
        } else {
            /* Vanilla */
            private _dmg = damage _u;
            if (_dmg < 0.01) then {
                _report = _name + " is fine.";
            } else {
                _report = format ["%1: %2%% health remaining.", _name, round ((1 - _dmg) * 100)];
            };
        };
    };

    _reports pushBack _report;
} forEach _netIds;

_reports joinString " "
