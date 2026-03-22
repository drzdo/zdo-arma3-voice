class CfgPatches {
    class zdo_arma_voice {
        name = "Zdo Arma Microphone";
        author = "drzdo";
        url = "https://github.com/drzdo/arma3-mic";
        units[] = {};
        weapons[] = {};
        requiredVersion = 2.14;
        requiredAddons[] = {"cba_main", "cba_settings"};
    };
};

class CfgFunctions {
    class zdo_arma_voice {
        class functions {
            file = "\zdo_arma_voice\functions";
            class init {
                preInit = 1;
            };
        };
    };
};
