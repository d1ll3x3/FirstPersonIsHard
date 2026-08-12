using System;
using System.Collections.Generic;
using EHS;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonFIH
{
    /// <summary>
    /// Hides the local phone while the camera sits inside it.
    ///
    /// Purely local: renderers are a client-side thing, so everyone else keeps seeing you.
    ///
    /// They are put into shadows-only rather than switched off, so your own shadow stays on
    /// the ground. From inside the phone that shadow is the one thing that still tells you
    /// where your body is and how high off the floor it is, which is worth keeping.
    /// The original mode of every renderer is cached: they do not all start the same.
    /// </summary>
    internal sealed class PlayerVisuals
    {
        private readonly List<Renderer> renderers = new();
        private ShadowCastingMode[] modes;
        private int ownerId;
        private bool hidden;

        public void Hide(PlayerRef player)
        {
            if (player == null)
            {
                return;
            }

            int id = player.GetInstanceID();

            // A respawn hands out a new player object with new renderers, so the cache from
            // the previous body must not be restored onto this one. Show() first in case
            // that old body is somehow still around, then start over on the new one.
            if (hidden && id != ownerId)
            {
                Show();
            }

            if (hidden)
            {
                return;
            }

            try
            {
                renderers.Clear();

                // The array overload, not the List<T> one: that wants an Il2CppSystem list.
                Renderer[] found = player.gameObject.GetComponentsInChildren<Renderer>(true);

                if (found != null)
                {
                    renderers.AddRange(found);
                }

                modes = new ShadowCastingMode[renderers.Count];

                for (int i = 0; i < renderers.Count; i++)
                {
                    modes[i] = renderers[i].shadowCastingMode;
                    renderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                }

                ownerId = id;
                hidden = true;
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Could not hide the player model: {ex}");
                Forget();
            }
        }

        public void Show()
        {
            if (!hidden)
            {
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                try
                {
                    Renderer renderer = renderers[i];

                    // Unity's == means a destroyed renderer compares as null here.
                    if (renderer != null)
                    {
                        renderer.shadowCastingMode = modes[i];
                    }
                }
                catch (Exception ex)
                {
                    FirstPersonPlugin.Logger.LogWarning($"A renderer could not be restored: {ex.Message}");
                }
            }

            Forget();
        }

        private void Forget()
        {
            renderers.Clear();
            modes = null;
            ownerId = 0;
            hidden = false;
        }
    }
}
