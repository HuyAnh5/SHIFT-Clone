using DarkTonic.PoolBoss;
using UnityEditor;
using UnityEngine;

namespace DarkTonic.PoolBoss.EditorScript
{
    // ReSharper disable once CheckNamespace
    [InitializeOnLoad]
    public class PoolBossWelcomeWindow : EditorWindow
    {
        private const string AddresablesSymbol = "ADDRESSABLES_ENABLED";

        private static bool showOnStartPrefs { // Records the customer's preference to show the window on start or not.
            get {
                return PoolBossSettings.Instance.ShowWelcomeWindowOnStart;
            }
            set {
                PoolBossSettings.Instance.ShowWelcomeWindowOnStart = value;
                EditorUtility.SetDirty(PoolBossSettings.Instance);
            }
        }
        public bool showOnStart = true;

        [MenuItem("Window/Pool Boss/Welcome Window", false, -2)]
        public static PoolBossWelcomeWindow ShowWindow()
        {
            var window = GetWindow<PoolBossWelcomeWindow>(false, "Welcome");
            var height = 213;

#if UNITY_2020_1_OR_NEWER
            height += 6;
#endif
            window.minSize = new Vector2(390, height);
            window.maxSize = new Vector2(390, height);
            window.showOnStart = true; // Can't check EditorPrefs when constructing window, so set this instead.
            return window;
        }

        [InitializeOnLoadMethod]
        private static void InitializeOnLoadMethod()
        {
            RegisterWindowCheck();
        }

        private static void RegisterWindowCheck()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update += CheckShowWelcomeWindow;
            }
        }

        private static void CheckShowWelcomeWindow()
        {
            EditorApplication.update -= CheckShowWelcomeWindow;
            if (showOnStartPrefs)
            {
                ShowWindow();
            }
        }

        void OnGUI()
        {
            DTPoolBossInspectorUtility.DrawUILine(DTPoolBossInspectorUtility.DividerColor);
            GUILayout.Label("Welcome to Pool Boss for Unity! The buttons below are shortcuts to commonly used help options.", EditorStyles.textArea);
            DTPoolBossInspectorUtility.DrawUILine(DTPoolBossInspectorUtility.DividerColor);

            GUILayout.Label("Help", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Manual", GUILayout.Width(90)))
            {
                Application.OpenURL("http://www.dtdevtools.com/downloads/poolboss/PB_ReadMe.pdf");
            }
            if (GUILayout.Button("Videos", GUILayout.Width(90)))
            {
                Application.OpenURL("https://www.youtube.com/watch?v=aBEkcHO6vZk&index=3&list=PLW6fMWQDKB24osBmTuJd0IG8R5tOim6eV");
            }
            if (GUILayout.Button("Scripting API", GUILayout.Width(90)))
            {
                Application.OpenURL("http://www.dtdevtools.com/API/poolboss/index.html");
            }
            if (GUILayout.Button("Support Forum", GUILayout.Width(100)))
            {
                Application.OpenURL("http://bit.ly/PioYZW");
            }
            EditorGUILayout.EndHorizontal();
            DTPoolBossInspectorUtility.DrawUILine(DTPoolBossInspectorUtility.DividerColor);

            GUILayout.Label("Optional package support", EditorStyles.boldLabel);
            GUILayout.Label("Enable support for:");

            // Addressables
            var enableAddress = DTPBDefineHelper.DoesScriptingDefineSymbolExist(AddresablesSymbol);
            var newAddress = GUILayout.Toggle(enableAddress, " Addressables (" + AddresablesSymbol + ")");
            if (newAddress != enableAddress)
            {
                if (newAddress)
                {
                    DTPBDefineHelper.TryAddScriptingDefineSymbols(AddresablesSymbol);
                }
                else
                {
                    DTPBDefineHelper.TryRemoveScriptingDefineSymbols(AddresablesSymbol);
                }
            }

            DTPoolBossInspectorUtility.ShowLargeBarAlert("Enabling a package you do not have installed will cause a compile error and you will not be able to use this window to undo until you install the missing package.");

            DTPoolBossInspectorUtility.DrawUILine(DTPoolBossInspectorUtility.DividerColor);

            EditorGUILayout.BeginHorizontal();
            var show = showOnStartPrefs;
            var newShow = GUILayout.Toggle(show, " Show at start");
            if (newShow != show)
            {
                showOnStartPrefs = newShow;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Email Support", "support@darktonic.com"), GUILayout.Width(100)))
            {
                Application.OpenURL("mailto:support@darktonic.com");
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}