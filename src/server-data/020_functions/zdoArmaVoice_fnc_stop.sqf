zdoArmaVoice_fnc_stop = {
params ["_netIds"];
{ doStop (_x call BIS_fnc_objectFromNetId) } forEach _netIds;
"ok"
}
