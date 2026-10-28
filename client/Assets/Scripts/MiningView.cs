using Rth.MapTools;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// What a miner sees over the rock they work (the "mining" server state, GatheringSystem):
    /// a bar filling towards the next ore, brighter while their cracked face lasts, and while a fire
    /// burns against the rock a thin line under it for their side of the stone's heat, its peak (the
    /// moment to douse) marked at the end. Drawn like the health bars, only for the miner.
    /// </summary>
    public sealed class MiningView : MonoBehaviour
    {
        public GameSession session;

        private static Texture2D _white;
        private MapObject _rock;
        private string _rockKey = "";
        private float _shown;          // the bar eases towards the server's progress

        private void OnGUI()
        {
            if (session == null || !session.IsInGame) return;
            MiningState m = session.ServerState<MiningState>(MiningState.StateKey);
            if (string.IsNullOrEmpty(m.Type))
            {
                _rock = null;
                _rockKey = "";
                return;
            }
            string key = m.Type + "@" + m.Plane + "," + m.X + "," + m.Y;
            if (key != _rockKey || _rock == null)
            {
                _rockKey = key;
                _rock = null;
                foreach (MapObject o in FindObjectsByType<MapObject>(FindObjectsInactive.Exclude))
                {
                    Vector2Int t = o.Tile;
                    if (o.objectType == m.Type && t.x == m.X && t.y == m.Y) { _rock = o; break; }
                }
                _shown = (float)m.Progress;
            }
            Camera cam = Camera.main;
            if (_rock == null || cam == null) return;

            float top = _rock.transform.position.y + 1f;
            foreach (Renderer r in _rock.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.name != "Outline") top = Mathf.Max(top, r.bounds.max.y);
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(_rock.transform.position.x, top + 0.35f, _rock.transform.position.z));
            if (screen.z <= 0f || Ui.UiRoot.IsPointerOverUi(screen)) return;
            if (_white == null) _white = Texture2D.whiteTexture;
            GUI.depth = 10;
            float x = screen.x, y = Screen.height - screen.y;

            // A new ore wraps the bar round: snap down, otherwise ease up towards it.
            float target = (float)m.Progress;
            _shown = target < _shown - 0.05f ? target : Mathf.MoveTowards(_shown, target, Time.deltaTime * 1.5f);
            bool cracked = m.Crack > 0;
            var back = new Rect(x - 26f, y - 4f, 52f, 7f);
            Fill(new Rect(back.x - 1f, back.y - 1f, back.width + 2f, back.height + 2f), new Color(0f, 0f, 0f, 0.7f));
            Fill(back, new Color(0.18f, 0.15f, 0.12f, 0.9f));
            Fill(new Rect(back.x, back.y, back.width * Mathf.Clamp01(_shown), back.height),
                cracked ? new Color(1f, 0.78f, 0.35f, 1f) : new Color(0.78f, 0.62f, 0.32f, 1f));
            if (cracked)       // what is left of the crack, a thin bright line along the top
                Fill(new Rect(back.x, back.y, back.width * (float)m.Crack, 1.5f), new Color(1f, 0.95f, 0.75f, 0.9f));

            if (m.Heat >= 0)
            {
                var heat = new Rect(back.x, back.yMax + 3f, back.width, 3f);
                Fill(new Rect(heat.x - 1f, heat.y - 1f, heat.width + 2f, heat.height + 2f), new Color(0f, 0f, 0f, 0.7f));
                Fill(new Rect(heat.xMax - heat.width * 0.06f, heat.y, heat.width * 0.06f, heat.height), new Color(1f, 0.9f, 0.6f, 0.5f)); // the peak
                Color ember = Color.Lerp(new Color(0.45f, 0.12f, 0.05f), new Color(1f, 0.45f, 0.12f), (float)m.Heat);
                if (m.Heat >= 0.97) ember = Color.Lerp(ember, Color.white, 0.35f + 0.25f * Mathf.Sin(Time.time * 10f));
                Fill(new Rect(heat.x, heat.y, heat.width * (float)m.Heat, heat.height), ember);
            }
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }
    }
}
