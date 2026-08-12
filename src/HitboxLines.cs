using System;
using EHS;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FirstPersonFIH
{
    /// <summary>
    /// Draws the edges of the player's collider from the inside.
    ///
    /// With the phone hidden there is nothing left on screen that belongs to your body, and
    /// a first person view with no body reads as floating. These lines rotate with the phone
    /// — that is the point: you can see how you are spinning without the view spinning with
    /// you, which is exactly the split the whole mod is built on.
    /// </summary>
    internal sealed class HitboxLines
    {
        // A cube is not one closed path, so three of the twelve edges are walked twice.
        // Bottom face 0..3, top face 4..7, with 4 above 0, 5 above 1 and so on.
        private static readonly int[] Path = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };

        private readonly Vector3[] corners = new Vector3[8];
        private readonly Vector3[] points = new Vector3[Path.Length];

        private GameObject host;
        private LineRenderer line;
        private bool unsupported;

        public void Tick(PlayerRef player, Settings settings)
        {
            if (unsupported || player == null)
            {
                return;
            }

            Collider collider = player.playerCollider;

            if (collider == null)
            {
                Clear();
                return;
            }

            try
            {
                if (!EnsureRenderer())
                {
                    return;
                }

                BuildCorners(collider);

                for (int i = 0; i < Path.Length; i++)
                {
                    points[i] = corners[Path[i]];
                }

                line.positionCount = points.Length;
                line.SetPositions(points);

                float width = Mathf.Max(0.001f, settings.HitboxLineWidth.Value);
                line.widthMultiplier = width;

                Color color = new(0.55f, 0.9f, 1f, Mathf.Clamp01(settings.HitboxOpacity.Value));
                line.startColor = color;
                line.endColor = color;
            }
            catch (Exception ex)
            {
                unsupported = true;
                Clear();
                FirstPersonPlugin.Logger.LogWarning($"Hitbox lines disabled, they could not be drawn: {ex.Message}");
            }
        }

        public void Clear()
        {
            if (host == null)
            {
                line = null;
                return;
            }

            Object.Destroy(host);
            host = null;
            line = null;
        }

        private bool EnsureRenderer()
        {
            if (line != null)
            {
                return true;
            }

            host = new GameObject("FirstPersonHitbox");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;

            line = host.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.receiveShadows = false;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.alignment = LineAlignment.View;

            // Nothing here can rely on a specific shader existing: the game is on URP, where
            // the old built-in line shaders may or may not be in the build.
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                unsupported = true;
                Clear();
                FirstPersonPlugin.Logger.LogWarning(
                    "No usable shader for the hitbox lines in this build, turning them off.");
                return false;
            }

            line.material = new Material(shader);
            return true;
        }

        /// <summary>
        /// The eight corners in world space. A box collider gives the real oriented box;
        /// anything else falls back to the world axis-aligned bounds, which do not rotate
        /// but still show where the body is.
        /// </summary>
        private void BuildCorners(Collider collider)
        {
            BoxCollider box = collider.TryCast<BoxCollider>();

            if (box != null)
            {
                Vector3 half = box.size * 0.5f;
                Vector3 center = box.center;
                Transform t = box.transform;

                corners[0] = t.TransformPoint(center + new Vector3(-half.x, -half.y, -half.z));
                corners[1] = t.TransformPoint(center + new Vector3(half.x, -half.y, -half.z));
                corners[2] = t.TransformPoint(center + new Vector3(half.x, -half.y, half.z));
                corners[3] = t.TransformPoint(center + new Vector3(-half.x, -half.y, half.z));
                corners[4] = t.TransformPoint(center + new Vector3(-half.x, half.y, -half.z));
                corners[5] = t.TransformPoint(center + new Vector3(half.x, half.y, -half.z));
                corners[6] = t.TransformPoint(center + new Vector3(half.x, half.y, half.z));
                corners[7] = t.TransformPoint(center + new Vector3(-half.x, half.y, half.z));
                return;
            }

            Bounds bounds = collider.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, min.y, max.z);
            corners[3] = new Vector3(min.x, min.y, max.z);
            corners[4] = new Vector3(min.x, max.y, min.z);
            corners[5] = new Vector3(max.x, max.y, min.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);
        }
    }
}
