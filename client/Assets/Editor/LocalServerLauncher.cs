using System.IO;
using System.Net.Sockets;
using Rth.MapTools;
using Rth.MapTools.Editor;
using Rth.Server;
using UnityEditor;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Dev convenience: pressing Play re-exports the scene's map and starts the real server core
    /// on background threads in the Editor process, so the client can log in to localhost.
    /// If the port is already taken (a standalone server, or another Editor such as a Multiplayer
    /// Play Mode clone), the client simply uses that server instead.
    /// </summary>
    [InitializeOnLoad]
    public static class LocalServerLauncher
    {
        private const string EnabledPref = "Rth.StartLocalServer";
        private const string MenuPath = "R'th/Start Local Server On Play";

        private static ServerHost _host;
        private static bool _triedThisPlay;

        static LocalServerLauncher()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopServer;
            EditorApplication.quitting += StopServer;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPref, true);
            set => EditorPrefs.SetBool(EnabledPref, value);
        }

        [MenuItem(MenuPath, priority = 20)]
        private static void ToggleEnabled() => Enabled = !Enabled;

        [MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Started before Play so the server is listening by the time the scene runs. If domain
            // reload on Play is ever turned on, the reload stops it and EnteredPlayMode restarts it.
            if ((change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode) && Enabled)
                StartServer();
            else if (change == PlayModeStateChange.EnteredEditMode)
                StopServer();
        }

        private static void StartServer()
        {
            if (_host != null || _triedThisPlay) return;
            _triedThisPlay = true;

            string exportName = "test";
            MapArea area = Object.FindAnyObjectByType<MapArea>();
            if (area != null)
            {
                MapExporter.Export(area); // keep the server's map in step with the scene being played
                exportName = area.exportName;
            }

            string serverDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "server"));
            var config = new ServerConfig
            {
                MapPath = Path.Combine(serverDir, "data", "maps", exportName + ".rthmap"),
                SaveDirectory = Path.Combine(serverDir, "saves"),
                ContentDirectory = Path.Combine(serverDir, "data", "content"),
                Log = line => Debug.Log("[Server] " + line),
            };
            if (!File.Exists(config.MapPath))
            {
                Debug.LogError("[Server] No map at " + config.MapPath + ". Open a scene with a MapArea and use R'th > Export Map.");
                return;
            }

            var host = new ServerHost(config);
            try
            {
                host.Start();
                _host = host;
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                Debug.Log("[Server] Port " + config.Port + " is already in use; connecting to the server that is running there instead.");
            }
        }

        private static void StopServer()
        {
            _triedThisPlay = false;
            if (_host == null) return;
            _host.Stop(); // saves every logged-in player
            _host = null;
        }
    }
}
