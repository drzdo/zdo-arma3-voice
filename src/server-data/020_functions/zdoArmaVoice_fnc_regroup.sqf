zdoArmaVoice_fnc_regroup = {
params ["_netIds"];
{ private _u = _x call BIS_fnc_objectFromNetId; _u enableAI "MOVE"; _u doFollow player } forEach _netIds;
"ok"
}
