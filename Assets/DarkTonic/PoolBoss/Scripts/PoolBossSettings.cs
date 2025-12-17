#if UNITY_EDITOR

/*! \cond PRIVATE */
namespace DarkTonic.PoolBoss {
    public class PoolBossSettings : PoolBossSingletonScriptable<PoolBossSettings> {
        public const string AssetName = "PoolBossSettings.asset";
        public const string AssetFolder = "Assets/Resources/PoolBoss";
        public const string ResourcePath = "PoolBoss/PoolBossSettings";
         
        public bool ShowWelcomeWindowOnStart = true;
         
        static PoolBossSettings()
        {
            AssetNameToLoad = string.Format("{0}/{1}", AssetFolder, AssetName);
            ResourceNameToLoad = ResourcePath;
            FoldersToCreate = new System.Collections.Generic.List<string> {
                "Assets/Resources",
                "Assets/Resources/PoolBoss"
            };
        }
    }
}
/*! \endcond */

#endif