using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;
using ClickThroughFix;
using ToolbarControl_NS;

namespace GForceDisplay
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class GForceToolbarRegistration : MonoBehaviour
    {
        private void Start()
        {
            ToolbarControl.RegisterMod(GForceDisplay.ModId, GForceDisplay.ModName);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class GForceDisplay : MonoBehaviour
    {
        internal const string ModId = "GForceDisplay_NS";
        internal const string ModName = "G-Force Display";

        private const int WindowId = 918273;
        private const int SettingsWindowId = 918274;
        private const double StandardGravity = 9.80665;
        private const float MinWindowWidth = 240f;
        private const float MinWindowHeight = 300f;
        private const float ResizeGripSize = 18f;

        private Rect windowRect = new Rect(300f, 120f, 280f, 390f);
        private Rect settingsRect = new Rect(590f, 120f, 300f, 270f);
        private bool windowVisible = true;
        private bool settingsVisible;
        private bool resizingMainWindow;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;

        private Texture2D whiteTexture;
        private GUIStyle centeredStyle;
        private GUIStyle largeValueStyle;
        private GUIStyle dialLabelStyle;
        private GUIStyle warningValueStyle;
        private GUIStyle redlineValueStyle;
        private PluginConfiguration config;
        private ToolbarControl toolbarControl;

        private float currentG;
        private float smoothedG;
        private float signedAxialG;
        private float peakPositiveG;
        private float peakNegativeG;
        private float observedMinG;
        private float observedMaxG;
        private bool haveObservedExtrema;

        private bool haveLastVelocity;
        private Vector3d lastVelocity;
        private Guid lastVesselId = Guid.Empty;

        private float dialMax = 10f;
        private float warningG = 4f;
        private float redlineG = 6f;
        private bool signedGMode;

        private string dialMaxText;
        private string warningGText;
        private string redlineGText;

        private void Awake()
        {
            CreateTextures();
            LoadSettings();
            ResetSettingText();
        }

        private void Start()
        {
            CreateToolbarButton();
        }

        private void OnDestroy()
        {
            SaveSettings();
            if (whiteTexture != null)
                Destroy(whiteTexture);
        }

        private void FixedUpdate()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
            {
                ResetAccelerationSampling();
                currentG = 0f;
                smoothedG = 0f;
                return;
            }

            if (vessel.id != lastVesselId)
            {
                lastVesselId = vessel.id;
                ResetPeaks();
                ResetAccelerationSampling();
            }

            UpdateSignedG(vessel);

            currentG = signedGMode ? signedAxialG : Mathf.Max(0f, (float)vessel.geeForce);
            float response = 1f - Mathf.Exp(-8f * Time.fixedDeltaTime);
            smoothedG = Mathf.Lerp(smoothedG, currentG, response);

            if (currentG > peakPositiveG)
                peakPositiveG = currentG;
            if (currentG < peakNegativeG)
                peakNegativeG = currentG;

            if (!haveObservedExtrema)
            {
                observedMinG = currentG;
                observedMaxG = currentG;
                haveObservedExtrema = true;
            }
            else
            {
                if (currentG < observedMinG)
                    observedMinG = currentG;
                if (currentG > observedMaxG)
                    observedMaxG = currentG;
            }
        }

        private void UpdateSignedG(Vessel vessel)
        {
            Vector3d velocity = vessel.obt_velocity;
            if (!haveLastVelocity)
            {
                lastVelocity = velocity;
                haveLastVelocity = true;
                signedAxialG = 0f;
                return;
            }

            double dt = Time.fixedDeltaTime;
            if (dt <= 0.000001)
                return;

            Vector3d worldAcceleration = (velocity - lastVelocity) / dt;
            lastVelocity = velocity;

            // Remove gravity to obtain the acceleration felt by the vessel/crew.
            Vector3d properAcceleration = worldAcceleration - vessel.graviticAcceleration;
            Vector3d axis = vessel.ReferenceTransform != null
                ? (Vector3d)vessel.ReferenceTransform.up
                : Vector3d.up;

            signedAxialG = (float)(Vector3d.Dot(properAcceleration, axis.normalized) / StandardGravity);

            // Reject extreme one-frame spikes caused by vessel unpacking, rails changes, etc.
            signedAxialG = Mathf.Clamp(signedAxialG, -100f, 100f);
        }

        private void ResetAccelerationSampling()
        {
            haveLastVelocity = false;
            lastVelocity = Vector3d.zero;
            signedAxialG = 0f;
        }

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            GUI.skin = HighLogic.Skin;
            EnsureStyles();

            if (windowVisible)
            {
                NormalizeMainWindowRect();
                windowRect = ClickThruBlocker.GUIWindow(
                    WindowId, windowRect, DrawWindow, "G-Force");
                NormalizeMainWindowRect();
                ClampWindow(ref windowRect);
            }

            if (settingsVisible)
            {
                settingsRect = ClickThruBlocker.GUILayoutWindow(
                    SettingsWindowId, settingsRect, DrawSettingsWindow, "G-Force Settings");
                ClampWindow(ref settingsRect);
            }
        }

        private void DrawWindow(int id)
        {
            float width = windowRect.width;
            float height = windowRect.height;
            const float margin = 10f;
            const float top = 24f;
            const float valueHeight = 38f;
            const float subtitleHeight = 20f;
            const float rangeHeight = 20f;
            const float peakHeight = 20f;
            const float buttonHeight = 28f;
            const float gap = 4f;

            GUIStyle valueStyle = GetValueStyle(currentG);
            GUI.Label(new Rect(margin, top, width - margin * 2f, valueHeight),
                string.Format("{0:+0.00;-0.00;0.00} G", currentG), valueStyle);

            float y = top + valueHeight;
            GUI.Label(new Rect(margin, y, width - margin * 2f, subtitleHeight),
                signedGMode ? "Signed axial acceleration load" : "Current acceleration load", centeredStyle);
            y += subtitleHeight + gap;

            float bottomReserved = rangeHeight + peakHeight + buttonHeight + gap * 4f + margin;
            float dialHeight = Mathf.Max(90f, height - y - bottomReserved);
            Rect dialRect = new Rect(margin, y, width - margin * 2f, dialHeight);
            DrawDial(dialRect, smoothedG);
            y += dialHeight + gap;

            GUI.Label(new Rect(margin, y, width - margin * 2f, rangeHeight),
                signedGMode
                    ? string.Format("Dial range: -{0:0.#} to +{0:0.#} G", dialMax)
                    : string.Format("Dial range: 0 to {0:0.#} G", dialMax), centeredStyle);
            y += rangeHeight;

            GUI.Label(new Rect(margin, y, width - margin * 2f, peakHeight),
                signedGMode
                    ? string.Format("Peaks: +{0:0.00} G / {1:0.00} G", peakPositiveG, peakNegativeG)
                    : string.Format("Peak: {0:0.00} G", peakPositiveG), centeredStyle);

            float buttonY = height - margin - buttonHeight;
            float buttonGap = 6f;
            float buttonWidth = (width - margin * 2f - buttonGap) * 0.5f;
            if (GUI.Button(new Rect(margin, buttonY, buttonWidth, buttonHeight), "Reset Peak"))
                ResetPeaks();
            if (GUI.Button(new Rect(margin + buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight),
                settingsVisible ? "Hide Settings" : "Settings"))
                settingsVisible = !settingsVisible;

            HandleMainWindowResize();

            // Make the whole window draggable. Normal controls still receive their clicks first,
            // and the bottom-right resize grip is reserved for resizing.
            if (!resizingMainWindow)
            {
                Rect dragRect = new Rect(0f, 0f, width, Mathf.Max(0f, height - ResizeGripSize));
                GUI.DragWindow(dragRect);
                GUI.DragWindow(new Rect(0f, height - ResizeGripSize,
                    Mathf.Max(0f, width - ResizeGripSize), ResizeGripSize));
            }
        }

        private void HandleMainWindowResize()
        {
            Rect grip = new Rect(windowRect.width - ResizeGripSize,
                windowRect.height - ResizeGripSize, ResizeGripSize, ResizeGripSize);

            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(WindowId + 1000, FocusType.Passive);

            if (e.type == EventType.Repaint)
            {
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.65f);
                for (int i = 0; i < 3; i++)
                {
                    float offset = 4f + i * 4f;
                    DrawLine(
                        new Vector2(grip.xMax - offset, grip.yMax - 2f),
                        new Vector2(grip.xMax - 2f, grip.yMax - offset),
                        1f, GUI.color);
                }
                GUI.color = old;
            }

            switch (e.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && grip.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        resizingMainWindow = true;
                        resizeStartMouse = e.mousePosition;
                        resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId && resizingMainWindow)
                    {
                        Vector2 delta = e.mousePosition - resizeStartMouse;
                        windowRect.width = Mathf.Max(MinWindowWidth, resizeStartSize.x + delta.x);
                        windowRect.height = Mathf.Max(MinWindowHeight, resizeStartSize.y + delta.y);
                        windowRect.width = Mathf.Min(windowRect.width, Mathf.Max(MinWindowWidth, Screen.width - windowRect.x));
                        windowRect.height = Mathf.Min(windowRect.height, Mathf.Max(MinWindowHeight, Screen.height - windowRect.y));
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        if (resizingMainWindow)
                        {
                            resizingMainWindow = false;
                            SaveSettings();
                            e.Use();
                        }
                    }
                    break;
            }
        }

        private void DrawSettingsWindow(int id)
        {
            GUILayout.Space(4f);

            bool newSignedMode = GUILayout.Toggle(signedGMode, "Signed axial G (- / +)");
            if (newSignedMode != signedGMode)
            {
                signedGMode = newSignedMode;
                ResetPeaks();
                ResetAccelerationSampling();
                SaveSettings();
            }

            GUILayout.Space(6f);
            DrawFloatSetting("Dial maximum G", ref dialMaxText);
            DrawFloatSetting("Warning starts at G", ref warningGText);
            DrawFloatSetting("Redline starts at G", ref redlineGText);

            GUILayout.Space(4f);
            GUILayout.Label("Warning/redline thresholds use absolute G in signed mode.", HighLogic.Skin.label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
                ApplySettingsFromText();
            if (GUILayout.Button("Defaults"))
            {
                dialMax = 10f;
                warningG = 4f;
                redlineG = 6f;
                signedGMode = false;
                ResetSettingText();
                ResetPeaks();
                ResetAccelerationSampling();
                SaveSettings();
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Close"))
            {
                settingsVisible = false;
                SaveSettings();
            }

            GUI.DragWindow(new Rect(0f, 0f, settingsRect.width, settingsRect.height));
        }

        private void DrawFloatSetting(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(175f));
            value = GUILayout.TextField(value, GUILayout.Width(80f));
            GUILayout.EndHorizontal();
        }

        private void ApplySettingsFromText()
        {
            float newDialMax;
            float newWarning;
            float newRedline;

            if (!float.TryParse(dialMaxText, out newDialMax))
                newDialMax = dialMax;
            if (!float.TryParse(warningGText, out newWarning))
                newWarning = warningG;
            if (!float.TryParse(redlineGText, out newRedline))
                newRedline = redlineG;

            dialMax = Mathf.Clamp(newDialMax, 0.5f, 100f);
            redlineG = Mathf.Clamp(newRedline, 0f, dialMax);
            warningG = Mathf.Clamp(newWarning, 0f, redlineG);

            ResetSettingText();
            SaveSettings();
        }

        private void ResetSettingText()
        {
            dialMaxText = dialMax.ToString("0.##");
            warningGText = warningG.ToString("0.##");
            redlineGText = redlineG.ToString("0.##");
        }

        private void DrawDial(Rect rect, float g)
        {
            float size = Mathf.Min(rect.width, rect.height) - 8f;
            Vector2 center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
            float radius = size * 0.43f;

            Color oldColor = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);
            GUI.DrawTexture(new Rect(center.x - radius - 10f, center.y - radius - 10f,
                (radius + 10f) * 2f, (radius + 10f) * 2f), whiteTexture);
            GUI.color = oldColor;

            const int divisions = 50;
            for (int i = 0; i <= divisions; i++)
            {
                float t = i / (float)divisions;
                float value = signedGMode
                    ? Mathf.Lerp(-dialMax, dialMax, t)
                    : Mathf.Lerp(0f, dialMax, t);
                bool major = (i % 5 == 0);
                float angle = Mathf.Lerp(-120f, 120f, t);
                float inner = radius - (major ? 16f : 9f);
                Color tickColor = GetThresholdColor(value);

                DrawRadialLine(center, inner, radius, angle, major ? 2f : 1f, tickColor);

                if (major)
                {
                    Vector2 labelPos = PointOnCircle(center, radius - 32f, angle);
                    Rect lr = new Rect(labelPos.x - 21f, labelPos.y - 10f, 42f, 20f);
                    string format = dialMax <= 5f ? "0.0" : "0";
                    GUI.Label(lr, value.ToString(format), dialLabelStyle);
                }
            }

            float normalized;
            if (signedGMode)
                normalized = Mathf.InverseLerp(-dialMax, dialMax, Mathf.Clamp(g, -dialMax, dialMax));
            else
                normalized = Mathf.InverseLerp(0f, dialMax, Mathf.Clamp(g, 0f, dialMax));

            // Draw observed min/max markers before the live needle so the current value
            // remains visually dominant. Markers stay attached to the dial scale as it resizes.
            if (haveObservedExtrema)
            {
                DrawExtremaMarker(center, radius, observedMinG, false);
                DrawExtremaMarker(center, radius, observedMaxG, true);
            }

            float needleAngle = Mathf.Lerp(-120f, 120f, normalized);
            DrawNeedle(center, radius - 23f, needleAngle, GetThresholdColor(g));

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - 5f, center.y - 5f, 10f, 10f), whiteTexture);
            GUI.color = oldColor;

            Rect valueRect = new Rect(center.x - 65f, center.y + radius * 0.46f, 130f, 28f);
            GUI.Label(valueRect, string.Format("{0:+0.00;-0.00;0.00} G", g), GetValueStyle(g));
        }


        private void DrawExtremaMarker(Vector2 center, float radius, float value, bool isMaximum)
        {
            float clamped = signedGMode
                ? Mathf.Clamp(value, -dialMax, dialMax)
                : Mathf.Clamp(value, 0f, dialMax);

            float normalized = signedGMode
                ? Mathf.InverseLerp(-dialMax, dialMax, clamped)
                : Mathf.InverseLerp(0f, dialMax, clamped);

            float angle = Mathf.Lerp(-120f, 120f, normalized);
            Color markerColor = isMaximum
                ? new Color(0.25f, 1f, 0.35f, 1f)
                : new Color(0.25f, 0.75f, 1f, 1f);

            // A thick radial mark just outside the normal ticks makes extrema easy to spot
            // without obscuring the scale labels or live needle.
            DrawRadialLine(center, radius + 2f, radius + 12f, angle, 4f, markerColor);

            Vector2 labelPos = PointOnCircle(center, radius + 23f, angle);
            string prefix = isMaximum ? "MAX " : "MIN ";
            Rect labelRect = new Rect(labelPos.x - 34f, labelPos.y - 9f, 68f, 18f);
            Color oldColor = dialLabelStyle.normal.textColor;
            dialLabelStyle.normal.textColor = markerColor;
            GUI.Label(labelRect, prefix + value.ToString("0.00"), dialLabelStyle);
            dialLabelStyle.normal.textColor = oldColor;
        }

        private Color GetThresholdColor(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (redlineG > 0f && magnitude >= redlineG)
                return new Color(1f, 0.25f, 0.15f, 1f);
            if (warningG > 0f && magnitude >= warningG)
                return new Color(1f, 0.8f, 0.15f, 1f);
            return Color.white;
        }

        private GUIStyle GetValueStyle(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (redlineG > 0f && magnitude >= redlineG)
                return redlineValueStyle;
            if (warningG > 0f && magnitude >= warningG)
                return warningValueStyle;
            return largeValueStyle;
        }

        private void DrawNeedle(Vector2 center, float length, float angle, Color color)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - length, 4f, length), whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawRadialLine(Vector2 center, float innerRadius, float outerRadius,
            float angle, float thickness, Color color)
        {
            Vector2 p1 = PointOnCircle(center, innerRadius, angle);
            Vector2 p2 = PointOnCircle(center, outerRadius, angle);
            DrawLine(p1, p2, thickness, color);
        }

        private static Vector2 PointOnCircle(Vector2 center, float radius, float angleDegrees)
        {
            float r = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(center.x + Mathf.Sin(r) * radius,
                center.y - Mathf.Cos(r) * radius);
        }

        private void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            Vector2 delta = b - a;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float length = delta.magnitude;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, length, thickness), whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void EnsureStyles()
        {
            if (centeredStyle != null)
                return;

            centeredStyle = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            largeValueStyle = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };

            warningValueStyle = new GUIStyle(largeValueStyle);
            warningValueStyle.normal.textColor = new Color(1f, 0.8f, 0.15f, 1f);

            redlineValueStyle = new GUIStyle(largeValueStyle);
            redlineValueStyle.normal.textColor = new Color(1f, 0.25f, 0.15f, 1f);

            dialLabelStyle = new GUIStyle(HighLogic.Skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }

        private void CreateTextures()
        {
            whiteTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            whiteTexture.SetPixel(0, 0, Color.white);
            whiteTexture.Apply();
        }

        private void CreateToolbarButton()
        {
            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(
                ToggleOn,
                ToggleOff,
                ApplicationLauncher.AppScenes.FLIGHT,
                ModId,
                "GForceDisplayButton",
                "GForceDisplay/PluginData/Textures/icon_38",
                "GForceDisplay/PluginData/Textures/icon_24",
                ModName);
        }

        private void ToggleOn()
        {
            windowVisible = true;
            SaveSettings();
        }

        private void ToggleOff()
        {
            windowVisible = false;
            settingsVisible = false;
            SaveSettings();
        }

        private void ResetPeaks()
        {
            peakPositiveG = 0f;
            peakNegativeG = 0f;
            observedMinG = 0f;
            observedMaxG = 0f;
            haveObservedExtrema = false;
        }


        private void NormalizeMainWindowRect()
        {
            // Protect against corrupt, legacy, NaN/Infinity, or otherwise unusable saved sizes.
            if (float.IsNaN(windowRect.width) || float.IsInfinity(windowRect.width) || windowRect.width < MinWindowWidth)
                windowRect.width = 280f;
            if (float.IsNaN(windowRect.height) || float.IsInfinity(windowRect.height) || windowRect.height < MinWindowHeight)
                windowRect.height = 390f;

            // Do not allow a restored window to be larger than the usable screen area.
            // On unusually small resolutions, retain as much of the minimum size as the screen permits.
            float maxWidth = Mathf.Max(120f, Screen.width - 10f);
            float maxHeight = Mathf.Max(120f, Screen.height - 10f);
            windowRect.width = Mathf.Clamp(windowRect.width, Mathf.Min(MinWindowWidth, maxWidth), maxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, Mathf.Min(MinWindowHeight, maxHeight), maxHeight);
        }

        private static void ClampWindow(ref Rect rect)
        {
            rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - 40f));
            rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - 30f));
        }

        private void LoadSettings()
        {
            try
            {
                config = PluginConfiguration.CreateForType<GForceDisplay>();
                config.load();

                windowRect.x = config.GetValue<float>("windowX", windowRect.x);
                windowRect.y = config.GetValue<float>("windowY", windowRect.y);
                windowRect.width = config.GetValue<float>("windowWidth", windowRect.width);
                windowRect.height = config.GetValue<float>("windowHeight", windowRect.height);
                settingsRect.x = config.GetValue<float>("settingsX", settingsRect.x);
                settingsRect.y = config.GetValue<float>("settingsY", settingsRect.y);
                windowVisible = config.GetValue<bool>("windowVisible", true);
                signedGMode = config.GetValue<bool>("signedGMode", false);
                dialMax = config.GetValue<float>("dialMax", 10f);
                warningG = config.GetValue<float>("warningG", 4f);
                redlineG = config.GetValue<float>("redlineG", 6f);

                NormalizeMainWindowRect();
                dialMax = Mathf.Clamp(dialMax, 0.5f, 100f);
                redlineG = Mathf.Clamp(redlineG, 0f, dialMax);
                warningG = Mathf.Clamp(warningG, 0f, redlineG);
            }
            catch (Exception ex)
            {
                Debug.LogError("[GForceDisplay] Failed to load settings: " + ex);
            }
        }

        private void SaveSettings()
        {
            try
            {
                NormalizeMainWindowRect();
                if (config == null)
                    config = PluginConfiguration.CreateForType<GForceDisplay>();

                config.SetValue("windowX", windowRect.x);
                config.SetValue("windowY", windowRect.y);
                config.SetValue("windowWidth", windowRect.width);
                config.SetValue("windowHeight", windowRect.height);
                config.SetValue("settingsX", settingsRect.x);
                config.SetValue("settingsY", settingsRect.y);
                config.SetValue("windowVisible", windowVisible);
                config.SetValue("signedGMode", signedGMode);
                config.SetValue("dialMax", dialMax);
                config.SetValue("warningG", warningG);
                config.SetValue("redlineG", redlineG);
                config.save();
            }
            catch (Exception ex)
            {
                Debug.LogError("[GForceDisplay] Failed to save settings: " + ex);
            }
        }
    }
}
