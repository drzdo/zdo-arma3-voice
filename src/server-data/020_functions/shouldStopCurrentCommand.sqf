zdoArmaVoice_fnc_shouldStopCurrentCommand = {
    params ["_unit", "_startTime"];
    !alive _unit
    || { _unit getVariable ["ace_medical_isUnconscious", false] }
    || { (_unit getVariable ["zdoArmaVoice_toldStopAt", 0]) > _startTime }
    || { (_unit getVariable ["zdoArmaVoice_toldRegroupAt", 0]) > _startTime }
    || { (_unit getVariable ["zdoArmaVoice_toldMoveAt", 0]) > _startTime }
}
