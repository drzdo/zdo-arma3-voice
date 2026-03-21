class CfgPatches {
    class arma3_mic {
        name = "ArmaVoice";
        author = "ArmaVoice";
        url = "";
        units[] = {};
        weapons[] = {};
        requiredVersion = 2.14;
        requiredAddons[] = {"cba_main", "cba_settings"};
    };
};

class CfgFunctions {
    class arma3_mic {
        class functions {
            file = "\arma3_mic\functions";
            class init {
                preInit = 1;
            };
        };
    };
};
