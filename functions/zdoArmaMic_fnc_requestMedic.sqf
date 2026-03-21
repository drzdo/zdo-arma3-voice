params ["_netIds"];
if (count _netIds == 0) then {
    [player] call ace_medical_ai_fnc_requestMedic;
} else {
    {
        private _u = _x call BIS_fnc_objectFromNetId;
        [_u] call ace_medical_ai_fnc_requestMedic;
    } forEach _netIds;
};
"ok"
