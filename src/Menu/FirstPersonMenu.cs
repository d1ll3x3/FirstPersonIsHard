using System;
using System.Collections.Generic;
using EHS;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FirstPersonFIH.Menu
{
    /// <summary>
    /// In-game settings menu, driven by the arrow keys.
    ///
    /// It exists because comfort cannot be tuned from a config file: the smoothing, the
    /// float and the field of view have to be moved while flipping, with the result on
    /// screen, or you are guessing. Every row edits a BepInEx ConfigEntry directly, so a
    /// value changed mid-run is already the saved value.
    ///
    /// Drawing is labels and filled rectangles only: GUI.Button and GUI.TextField are
    /// stripped in this IL2CPP build and crash the game, and keys are read through the
    /// Input System in Update like the rest of the mod rather than from the IMGUI events.
    /// </summary>
    internal sealed class FirstPersonMenu
    {
        private const float RowHeight = 22f;
        private const float Width = 460f;
        private const float Padding = 18f;

        private readonly Settings settings;
        private readonly List<MenuRow> rows = new();

        private int selected;
        private float upRepeat;
        private float downRepeat;
        private float leftRepeat;
        private float rightRepeat;

        private CursorLockMode savedLockState = CursorLockMode.Locked;
        private bool savedCursorVisible;
        private bool blockingGameInput;

        // Blocker token for the game's own jump block list, created on first use.
        private Il2CppSystem.Object jumpToken;

        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle valueStyle;
        private GUIStyle headerStyle;
        private GUIStyle hintStyle;

        public FirstPersonMenu(Settings settings)
        {
            this.settings = settings;
            Build();
            selected = FirstSelectable();
        }

        public bool IsOpen { get; private set; }

        public void Toggle()
        {
            IsOpen = !IsOpen;

            if (IsOpen)
            {
                // Remembered rather than assumed, so closing the menu from a screen that
                // already had a free cursor does not lock it away.
                savedLockState = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            CancelCapture();
            settings.Save();
            SetGameInputBlocked(false);
            Cursor.lockState = savedLockState;
            Cursor.visible = savedCursorVisible;
        }

        public void Tick()
        {
            if (!IsOpen)
            {
                return;
            }

            // The game grabs the cursor back every frame, so it has to be re-released.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Re-asserted every frame rather than only on open: the player can respawn into
            // a fresh instance while the menu is up, and the block lives on the instance.
            SetGameInputBlocked(true);

            if (rows[selected] is KeyRow capturing && capturing.Capturing)
            {
                capturing.Capture();
                return;
            }

            if (InputReader.WasPressedOrRepeating(Key.UpArrow, ref upRepeat))
            {
                Step(-1);
            }

            if (InputReader.WasPressedOrRepeating(Key.DownArrow, ref downRepeat))
            {
                Step(1);
            }

            if (InputReader.WasPressedOrRepeating(Key.LeftArrow, ref leftRepeat))
            {
                rows[selected].Adjust(-1);
            }

            if (InputReader.WasPressedOrRepeating(Key.RightArrow, ref rightRepeat))
            {
                rows[selected].Adjust(1);
            }

            if (InputReader.WasPressedThisFrame(Key.Enter) && rows[selected] is KeyRow key)
            {
                key.Adjust(0);
            }

            if (InputReader.WasPressedThisFrame(Key.R))
            {
                rows[selected].Reset();
            }
        }

        public void Draw()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureStyles();

            float height = Padding * 2f + RowHeight * (rows.Count + 3);
            float x = (Screen.width - Width) * 0.5f;
            float y = Mathf.Max(20f, (Screen.height - height) * 0.5f);

            Gui.Fill(new Rect(x, y, Width, height), Gui.Backdrop);

            float rowX = x + Padding;
            float rowWidth = Width - Padding * 2f;
            float cursorY = y + Padding;

            GUI.Label(new Rect(rowX, cursorY, rowWidth, RowHeight), "FIRST PERSON IS HARD", titleStyle);
            cursorY += RowHeight * 1.4f;

            for (int i = 0; i < rows.Count; i++)
            {
                MenuRow row = rows[i];
                var rect = new Rect(rowX, cursorY, rowWidth, RowHeight);

                if (i == selected)
                {
                    Gui.Fill(rect, Gui.Selection);
                }

                if (row.Selectable)
                {
                    GUI.Label(rect, row.Label, labelStyle);
                    GUI.Label(rect, row.Value, valueStyle);
                }
                else
                {
                    GUI.Label(rect, row.Label, headerStyle);
                }

                cursorY += RowHeight;
            }

            cursorY += RowHeight * 0.3f;
            GUI.Label(new Rect(rowX, cursorY, rowWidth, RowHeight),
                $"arrows = move / change   ·   R = default   ·   {settings.MenuKey.Value} = close",
                hintStyle);
        }

        /// <summary>Called on unload so a menu left open does not keep the game's input blocked.</summary>
        public void Close()
        {
            if (IsOpen)
            {
                Toggle();
            }
        }

        private void Build()
        {
            rows.Add(new HeaderRow("EYE"));
            rows.Add(new FloatRow("Height above centre of mass", settings.EyeOffsetY, 0.01f, -0.5f, 1f));
            rows.Add(new FloatRow("Forward offset", settings.EyeOffsetZ, 0.01f, -0.5f, 0.5f));
            rows.Add(new FloatRow("Side offset", settings.EyeOffsetX, 0.01f, -0.5f, 0.5f));
            rows.Add(new FloatRow("Field of view", settings.FieldOfView, 1f, 40f, 120f, "0"));

            rows.Add(new HeaderRow("COMFORT"));
            rows.Add(new FloatRow("Horizontal smoothing (s)", settings.HorizontalSmoothing, 0.01f, 0f, 0.5f));
            rows.Add(new FloatRow("Vertical smoothing (s)", settings.VerticalSmoothing, 0.01f, 0f, 0.5f));
            rows.Add(new FloatRow("Max vertical lag (m)", settings.MaxVerticalLag, 0.1f, 0f, 3f));
            rows.Add(new FloatRow("Teleport cut distance (m)", settings.TeleportSnapDistance, 0.5f, 1f, 30f));
            rows.Add(new FloatRow("Comfort vignette", settings.ComfortVignette, 0.05f, 0f, 1f));
            rows.Add(new BoolRow("Centre dot", settings.CenterDot));
            rows.Add(new BoolRow("Hide speed lines", settings.DisableSpeedLines));

            rows.Add(new HeaderRow("BODY"));
            rows.Add(new BoolRow("Hide the phone", settings.HidePlayerModel));
            rows.Add(new BoolRow("Hitbox lines", settings.ShowHitboxLines));
            rows.Add(new FloatRow("Hitbox line width (m)", settings.HitboxLineWidth, 0.002f, 0.002f, 0.05f));
            rows.Add(new FloatRow("Hitbox opacity", settings.HitboxOpacity, 0.05f, 0f, 1f));

            rows.Add(new HeaderRow("LOOK & BINDS"));
            rows.Add(new BoolRow("Widen look range", settings.ExtendPitchRange));
            rows.Add(new FloatRow("Look range (deg)", settings.PitchLimit, 5f, 10f, 89f, "0"));
            rows.Add(new BoolRow("Corner readout", settings.ShowStatus));
            rows.Add(new KeyRow("First person key", settings.ToggleKey));
            rows.Add(new KeyRow("Menu key", settings.MenuKey));
        }

        private void Step(int direction)
        {
            CancelCapture();

            for (int i = 0; i < rows.Count; i++)
            {
                selected = (selected + direction + rows.Count) % rows.Count;

                if (rows[selected].Selectable)
                {
                    return;
                }
            }
        }

        private int FirstSelectable()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Selectable)
                {
                    return i;
                }
            }

            return 0;
        }

        private void CancelCapture()
        {
            if (rows[selected] is KeyRow key)
            {
                key.CancelCapture();
            }
        }

        /// <summary>
        /// Stops the arrow keys and the rest from reaching the game while the menu is up.
        /// </summary>
        private void SetGameInputBlocked(bool blocked)
        {
            if (!blocked && !blockingGameInput)
            {
                return;
            }

            try
            {
                PlayerRef player = GameRefs.LocalPlayer;

                if (player?.Movement != null)
                {
                    player.Movement.BlockInput = blocked;
                }

                if (player?.MovementJump != null)
                {
                    jumpToken ??= new Il2CppSystem.Object();

                    if (blocked)
                    {
                        player.MovementJump.AddJumpBlock(jumpToken);
                    }
                    else
                    {
                        player.MovementJump.RemoveJumpBlock(jumpToken);
                    }
                }

                blockingGameInput = blocked;
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Could not {(blocked ? "block" : "restore")} game input: {ex}");
            }
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = Gui.Style(16, FontStyle.Bold, Gui.Accent, TextAnchor.UpperLeft);
            labelStyle = Gui.Style(13, FontStyle.Normal, Gui.Normal, TextAnchor.MiddleLeft);
            valueStyle = Gui.Style(13, FontStyle.Bold, Gui.Highlight, TextAnchor.MiddleRight);
            headerStyle = Gui.Style(12, FontStyle.Bold, Gui.Dimmed, TextAnchor.LowerLeft);
            hintStyle = Gui.Style(11, FontStyle.Normal, Gui.Dimmed, TextAnchor.MiddleLeft);
        }
    }
}
