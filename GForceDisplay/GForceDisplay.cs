using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;
using ClickThroughFix;
using ToolbarControl_NS;

namespace GForceDisplay
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        internal static GUIStyle styleXButtonSettings;
        bool initialized = false;

        private void Start()
        {
            ToolbarControl.RegisterMod(GForceDisplay.ModId, GForceDisplay.ModName);
        }

        void InitWinTextures()
        {
            styleXButtonSettings = new GUIStyle(GUI.skin.button);
            styleXButtonSettings.normal.textColor = GUI.skin.button.normal.textColor;
            styleXButtonSettings.hover.textColor = GUI.skin.button.hover.textColor;

            styleXButtonSettings.name = "ButtonSettings";
            styleXButtonSettings.padding = new RectOffset(1, 1, 1, 1);
            styleXButtonSettings.onNormal.background = styleXButtonSettings.active.background;
            styleXButtonSettings.alignment = TextAnchor.MiddleCenter;
            styleXButtonSettings.normal.textColor = new Color32(177, 193, 205, 255);
            styleXButtonSettings.fontStyle = FontStyle.Bold;

        }
        private void OnGUI()
        {
            if (!initialized)
            {
                // Capture the stock Unity skin before anything overrides it, then apply
                // whichever skin the settings ask for.
                initialized = true;

                InitWinTextures();
            }
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
        private const float MinWindowWidth = 360f;
        private const float MinWindowHeight = 360f;
        private const float EmptyWindowMinWidth = 240f;
        private const float EmptyWindowMinHeight = 180f;
        private const float GraphOnlyMinWidth = 180f;
        private const float GraphOnlyMinHeight = 180f;
        private const float ResizeGripSize = 18f;

        private Rect windowRect = new Rect(300f, 120f, 420f, 460f);
        private Rect settingsRect = new Rect(590f, 120f, 300f, 370f);
        private bool windowVisible = false;
        private bool settingsVisible;
        private int settingsAlignmentFrames;
        private bool uiHidden;
        private bool hideWhenPaused = true;
        private bool showDial = true;
        private bool totalGAsVerticalGraph;
        private bool showVerticalG = true;
        private bool showHorizontalG = true;
        private bool graphOnlyWhenTotalGOnly;
        private Color barFillColor = new Color(0.20f, 0.70f, 1.00f, 0.60f);
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
        private float verticalG;
        private float horizontalG;
        private float smoothedVerticalG;
        private float smoothedHorizontalG;
        private float peakPositiveG;
        private float peakNegativeG;
        private float observedMinG;
        private float observedMaxG;
        private bool haveObservedExtrema;
        private float observedMinVerticalG;
        private float observedMaxVerticalG;
        private bool haveObservedVerticalExtrema;
        private float observedMinHorizontalG;
        private float observedMaxHorizontalG;
        private bool haveObservedHorizontalExtrema;

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
        private string barFillRText;
        private string barFillGText;
        private string barFillBText;

        private void Awake()
        {
            CreateTextures();
            LoadSettings();
            ResetSettingText();

            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        private void Start()
        {
            CreateToolbarButton();
        }

        private void OnDestroy()
        {
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);

            SaveSettings();
            if (whiteTexture != null)
                Destroy(whiteTexture);
        }

        private void OnHideUI()
        {
            // F2 / stock Hide UI must always suppress every G-Force Display window.
            // Do not change windowVisible/settingsVisible here so they can return when the UI is shown.
            uiHidden = true;
        }

        private void OnShowUI()
        {
            uiHidden = false;
        }

        private bool ShouldSuppressWindows()
        {
            if (uiHidden)
                return true;

            return hideWhenPaused && Time.timeScale <= 0.0001f;
        }

        private void FixedUpdate()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (!HighLogic.LoadedSceneIsFlight || vessel == null)
            {
                ResetAccelerationSampling();
                currentG = 0f;
                smoothedG = 0f;
                verticalG = 0f;
                horizontalG = 0f;
                smoothedVerticalG = 0f;
                smoothedHorizontalG = 0f;
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
            smoothedVerticalG = Mathf.Lerp(smoothedVerticalG, verticalG, response);
            smoothedHorizontalG = Mathf.Lerp(smoothedHorizontalG, horizontalG, response);

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

            if (!haveObservedVerticalExtrema)
            {
                observedMinVerticalG = verticalG;
                observedMaxVerticalG = verticalG;
                haveObservedVerticalExtrema = true;
            }
            else
            {
                if (verticalG < observedMinVerticalG)
                    observedMinVerticalG = verticalG;
                if (verticalG > observedMaxVerticalG)
                    observedMaxVerticalG = verticalG;
            }

            if (!haveObservedHorizontalExtrema)
            {
                observedMinHorizontalG = horizontalG;
                observedMaxHorizontalG = horizontalG;
                haveObservedHorizontalExtrema = true;
            }
            else
            {
                if (horizontalG < observedMinHorizontalG)
                    observedMinHorizontalG = horizontalG;
                if (horizontalG > observedMaxHorizontalG)
                    observedMaxHorizontalG = horizontalG;
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

            // Resolve the felt acceleration into a local surface frame.  Vertical G is signed:
            // positive is away from the current main body's center and negative is downward.
            // Horizontal G is the magnitude in the plane perpendicular to local vertical.
            Vector3d verticalAxis = vessel.upAxis;
            if (verticalAxis.sqrMagnitude < 0.000001)
                verticalAxis = axis;
            verticalAxis = verticalAxis.normalized;

            double verticalAccel = Vector3d.Dot(properAcceleration, verticalAxis);
            Vector3d horizontalAccel = properAcceleration - verticalAxis * verticalAccel;
            verticalG = (float)(verticalAccel / StandardGravity);
            horizontalG = (float)(horizontalAccel.magnitude / StandardGravity);

            // Reject extreme one-frame spikes caused by vessel unpacking, rails changes, etc.
            signedAxialG = Mathf.Clamp(signedAxialG, -100f, 100f);
            verticalG = Mathf.Clamp(verticalG, -100f, 100f);
            horizontalG = Mathf.Clamp(horizontalG, 0f, 100f);
        }

        private void ResetAccelerationSampling()
        {
            haveLastVelocity = false;
            lastVelocity = Vector3d.zero;
            signedAxialG = 0f;
            verticalG = 0f;
            horizontalG = 0f;
            smoothedVerticalG = 0f;
            smoothedHorizontalG = 0f;
        }

        private void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            GUI.skin = HighLogic.Skin;
            EnsureStyles();

            if (ShouldSuppressWindows())
                return;

            if (windowVisible)
            {
                NormalizeMainWindowRect();
                windowRect = ClickThruBlocker.GUIWindow(
                    WindowId, windowRect, DrawWindow, IsGraphOnlyMode() ? string.Empty : "G-Force");
                NormalizeMainWindowRect();
                ClampWindow(ref windowRect);
            }

            if (settingsVisible)
            {
                settingsRect = ClickThruBlocker.GUILayoutWindow(
                    SettingsWindowId, settingsRect, DrawSettingsWindow, "G-Force Settings");

                // GUILayoutWindow can adjust its size over the first few layout passes.
                // Keep the window aligned while it settles, then allow normal independent dragging.
                if (settingsAlignmentFrames > 0)
                {
                    AlignSettingsWindowToMain();
                    settingsAlignmentFrames--;
                }
                else
                {
                    ClampWindow(ref settingsRect);
                }
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
            const float verticalGraphWidth = 100f;
            const float horizontalGraphHeight = 82f;

            if (GUI.Button(new Rect(windowRect.width - 32, 2, 30, 20), "×", RegisterToolbar.styleXButtonSettings))
            {
                ToggleOff();
                toolbarControl.SetFalse();
            }

                if (IsGraphOnlyMode())
            {
                Rect graphRect = new Rect(6f, 6f, Mathf.Max(40f, width - 12f), Mathf.Max(40f, height - 12f));
                if (totalGAsVerticalGraph)
                    DrawTotalGVerticalSlider(graphRect, smoothedG);
                else
                    DrawDial(graphRect, smoothedG);

                // With all visible controls hidden, right-click the graph to reopen Settings.
                Event graphOnlyEvent = Event.current;
                if (graphOnlyEvent.type == EventType.MouseDown && graphOnlyEvent.button == 1 &&
                    new Rect(0f, 0f, width, height).Contains(graphOnlyEvent.mousePosition))
                {
                    settingsVisible = true;
                    AlignSettingsWindowToMain();
                    settingsAlignmentFrames = 3;
                    graphOnlyEvent.Use();
                }

                HandleMainWindowResize();
                if (!resizingMainWindow)
                {
                    Rect dragRect = new Rect(0f, 0f, width, Mathf.Max(0f, height - ResizeGripSize));
                    GUI.DragWindow(dragRect);
                    GUI.DragWindow(new Rect(0f, height - ResizeGripSize,
                        Mathf.Max(0f, width - ResizeGripSize), ResizeGripSize));
                }
                return;
            }

            GUIStyle valueStyle = GetValueStyle(currentG);
            GUI.Label(new Rect(margin, top, width - margin * 2f, valueHeight),
                string.Format("{0:+0.00;-0.00;0.00} G", currentG), valueStyle);

            float y = top + valueHeight;
            GUI.Label(new Rect(margin, y, width - margin * 2f, subtitleHeight),
                signedGMode ? "Signed axial acceleration load" : "Current acceleration load", centeredStyle);
            y += subtitleHeight + gap;

            float buttonY = height - margin - buttonHeight;
            float peakY = buttonY - gap - peakHeight;
            float rangeY = peakY - rangeHeight;

            // When the main dial is hidden, show enabled auxiliary graphs as vertical
            // sliders. If both are enabled, they share the available width side by side.
            if (!showDial)
            {
                float contentBottom = rangeY - gap;
                float contentHeight = Mathf.Max(0f, contentBottom - y);
                int graphCount = (showVerticalG ? 1 : 0) + (showHorizontalG ? 1 : 0);

                if (graphCount > 0)
                {
                    contentHeight = Mathf.Max(120f, contentHeight);
                    float totalGap = graphCount > 1 ? gap : 0f;
                    float graphWidth = Mathf.Max(100f, (width - margin * 2f - totalGap) / graphCount);
                    float graphX = margin;

                    if (showVerticalG)
                    {
                        DrawVerticalGSlider(new Rect(graphX, y, graphWidth, contentHeight), smoothedVerticalG);
                        graphX += graphWidth + gap;
                    }

                    if (showHorizontalG)
                    {
                        DrawHorizontalGVerticalSlider(new Rect(graphX, y, graphWidth, contentHeight), smoothedHorizontalG);
                    }
                }
            }
            else
            {
                float horizontalGraphY = showHorizontalG ? rangeY - gap - horizontalGraphHeight : rangeY;
                float contentBottom = showHorizontalG ? horizontalGraphY - gap : rangeY - gap;
                float contentHeight = Mathf.Max(120f, contentBottom - y);

                float dialX = margin;
                if (showVerticalG)
                {
                    Rect verticalRect = new Rect(margin, y, verticalGraphWidth, contentHeight);
                    DrawVerticalGSlider(verticalRect, smoothedVerticalG);
                    dialX += verticalGraphWidth + gap;
                }

                float dialWidth = Mathf.Max(140f, width - dialX - margin);
                Rect dialRect = new Rect(dialX, y, dialWidth, contentHeight);
                if (totalGAsVerticalGraph)
                    DrawTotalGVerticalSlider(dialRect, smoothedG);
                else
                    DrawDial(dialRect, smoothedG);

                if (showHorizontalG)
                {
                    float horizontalX = dialX;

                    if (totalGAsVerticalGraph && showVerticalG)
                        horizontalX = Mathf.Max(margin, (width - dialWidth) * 0.5f);

                    Rect horizontalRect = new Rect(horizontalX, horizontalGraphY, dialWidth, horizontalGraphHeight);
                    DrawHorizontalGSlider(horizontalRect, smoothedHorizontalG);
                }
            }

            GUI.Label(new Rect(margin, rangeY, width - margin * 2f, rangeHeight),
                signedGMode
                    ? string.Format("Dial range: -{0:0.#} to +{0:0.#} G", dialMax)
                    : string.Format("Dial range: 0 to {0:0.#} G", dialMax), centeredStyle);

            GUI.Label(new Rect(margin, peakY, width - margin * 2f, peakHeight),
                signedGMode
                    ? string.Format("Peaks: +{0:0.00} G / {1:0.00} G", peakPositiveG, peakNegativeG)
                    : string.Format("Peak: {0:0.00} G", peakPositiveG), centeredStyle);

            float buttonGap = 6f;
            float buttonWidth = Mathf.Min(110f, (width - margin * 2f - buttonGap) * 0.5f);
            float buttonsWidth = buttonWidth * 2f + buttonGap;
            float buttonsX = (width - buttonsWidth) * 0.5f;
            if (GUI.Button(new Rect(buttonsX, buttonY, buttonWidth, buttonHeight), "Reset Peak"))
                ResetPeaks();
            if (GUI.Button(new Rect(buttonsX + buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight),
                settingsVisible ? "Hide Settings" : "Settings"))
            {
                bool openingSettings = !settingsVisible;
                settingsVisible = !settingsVisible;
                if (openingSettings)
                {
                    AlignSettingsWindowToMain();
                    settingsAlignmentFrames = 3;
                }
            }

            HandleMainWindowResize();

            if (!resizingMainWindow)
            {
                Rect dragRect = new Rect(0f, 0f, width, Mathf.Max(0f, height - ResizeGripSize));
                GUI.DragWindow(dragRect);
                GUI.DragWindow(new Rect(0f, height - ResizeGripSize,
                    Mathf.Max(0f, width - ResizeGripSize), ResizeGripSize));
            }
        }

        private void DrawVerticalGSlider(Rect rect, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), "Vertical", centeredStyle);

            Color maxColor = new Color(0.25f, 1f, 0.35f, 1f);
            Color minColor = new Color(0.25f, 0.75f, 1f, 1f);
            Color oldTextColor = dialLabelStyle.normal.textColor;

            if (haveObservedVerticalExtrema)
            {
                dialLabelStyle.normal.textColor = maxColor;
                GUI.Label(new Rect(rect.x, rect.y + 17f, rect.width, 18f),
                    "MAX " + observedMaxVerticalG.ToString("+0.00;-0.00;0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = minColor;
                GUI.Label(new Rect(rect.x, rect.yMax - 38f, rect.width, 18f),
                    "MIN " + observedMinVerticalG.ToString("+0.00;-0.00;0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = oldTextColor;
            }

            float trackTop = rect.y + 38f;
            float trackBottom = rect.yMax - 42f;
            float trackHeight = Mathf.Max(40f, trackBottom - trackTop);
            float trackWidth = Mathf.Min(22f, rect.width * 0.28f);
            float trackX = rect.x + rect.width * 0.62f - trackWidth * 0.5f;
            Rect track = new Rect(trackX, trackTop, trackWidth, trackHeight);

            Color old = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);
            GUI.DrawTexture(track, whiteTexture);

            // Scale markings: ten intervals, with labels on every second tick.
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float scaleValue = Mathf.Lerp(-dialMax, dialMax, t);
                float tickY = Mathf.Lerp(track.yMax, track.y, t);
                bool major = (i % 2 == 0) || i == 5;
                float tickLength = major ? 8f : 4f;

                GUI.color = new Color(1f, 1f, 1f, major ? 0.70f : 0.35f);
                // Keep vertical scale marks short and entirely to the left of the track.
                GUI.DrawTexture(new Rect(track.x - tickLength - 2f, tickY - 1f,
                    tickLength, major ? 2f : 1f), whiteTexture);

                if (major)
                {
                    string format = dialMax <= 5f ? "0.0" : "0";
                    string label = i == 5 ? "0" : scaleValue.ToString(format);
                    GUI.Label(new Rect(track.x - 48f, tickY - 9f, 36f, 18f),
                        label, dialLabelStyle);
                }
            }

            float zeroY = track.y + track.height * 0.5f;

            // Fill from 0 G to the current signed vertical-G value.
            float fillClamped = Mathf.Clamp(value, -dialMax, dialMax);
            float fillNormalized = Mathf.InverseLerp(-dialMax, dialMax, fillClamped);
            float fillY = Mathf.Lerp(track.yMax, track.y, fillNormalized);
            float fillTop = Mathf.Min(zeroY, fillY);
            float fillHeight = Mathf.Abs(fillY - zeroY);
            if (fillHeight > 0.5f)
            {
                GUI.color = barFillColor;
                GUI.DrawTexture(new Rect(track.x, fillTop, track.width, fillHeight), whiteTexture);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.80f);
            GUI.DrawTexture(new Rect(track.x - 9f, zeroY - 1f, track.width + 18f, 2f), whiteTexture);

            // Observed minimum and maximum markers.
            if (haveObservedVerticalExtrema)
            {
                float minT = Mathf.InverseLerp(-dialMax, dialMax,
                    Mathf.Clamp(observedMinVerticalG, -dialMax, dialMax));
                float maxT = Mathf.InverseLerp(-dialMax, dialMax,
                    Mathf.Clamp(observedMaxVerticalG, -dialMax, dialMax));
                float minY = Mathf.Lerp(track.yMax, track.y, minT);
                float maxY = Mathf.Lerp(track.yMax, track.y, maxT);

                // Keep extrema markers short and to the left of the vertical track.
                const float extremaMarkerLength = 12f;
                GUI.color = minColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, minY - 2f, extremaMarkerLength, 4f), whiteTexture);
                GUI.color = maxColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, maxY - 2f, extremaMarkerLength, 4f), whiteTexture);
            }

            float clamped = Mathf.Clamp(value, -dialMax, dialMax);
            float normalized = Mathf.InverseLerp(-dialMax, dialMax, clamped);
            float markerY = Mathf.Lerp(track.yMax, track.y, normalized);
            GUI.color = GetThresholdColor(value);
            GUI.DrawTexture(new Rect(track.x - 9f, markerY - 3f, track.width + 18f, 6f), whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(rect.x, rect.yMax - 20f, rect.width, 20f),
                string.Format("{0:+0.00;-0.00;0.00} G", value), centeredStyle);
        }

        private void DrawTotalGVerticalSlider(Rect rect, float value, bool hideText = false)
        {
            if (!hideText)
                GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), "Total G", centeredStyle);

            Color maxColor = new Color(0.25f, 1f, 0.35f, 1f);
            Color minColor = new Color(0.25f, 0.75f, 1f, 1f);
            Color oldTextColor = dialLabelStyle.normal.textColor;

            if (haveObservedExtrema && !hideText)
            {
                dialLabelStyle.normal.textColor = maxColor;
                GUI.Label(new Rect(rect.x, rect.y + 17f, rect.width, 18f),
                    "MAX " + observedMaxG.ToString("+0.00;-0.00;0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = minColor;
                GUI.Label(new Rect(rect.x, rect.yMax - 38f, rect.width, 18f),
                    "MIN " + observedMinG.ToString("+0.00;-0.00;0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = oldTextColor;
            }

            float trackTop = rect.y + (hideText ? 8f : 38f);
            float trackBottom = rect.yMax - (hideText ? 8f : 42f);
            float trackHeight = Mathf.Max(40f, trackBottom - trackTop);
            float trackWidth = Mathf.Min(22f, rect.width * 0.28f);
            float trackX = rect.x + rect.width * (hideText ? 0.5f : 0.62f) - trackWidth * 0.5f;
            Rect track = new Rect(trackX, trackTop, trackWidth, trackHeight);

            float minScale = signedGMode ? -dialMax : 0f;
            float maxScale = dialMax;

            Color old = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);
            GUI.DrawTexture(track, whiteTexture);

            float zeroT = Mathf.InverseLerp(minScale, maxScale, 0f);
            float zeroY = Mathf.Lerp(track.yMax, track.y, zeroT);

            float fillClamped = Mathf.Clamp(value, minScale, maxScale);
            float fillNormalized = Mathf.InverseLerp(minScale, maxScale, fillClamped);
            float fillY = Mathf.Lerp(track.yMax, track.y, fillNormalized);
            float fillTop = Mathf.Min(zeroY, fillY);
            float fillHeight = Mathf.Abs(fillY - zeroY);
            if (fillHeight > 0.5f)
            {
                GUI.color = barFillColor;
                GUI.DrawTexture(new Rect(track.x, fillTop, track.width, fillHeight), whiteTexture);
            }

            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float scaleValue = Mathf.Lerp(minScale, maxScale, t);
                float tickY = Mathf.Lerp(track.yMax, track.y, t);
                bool zeroTick = Mathf.Abs(scaleValue) < (dialMax / 20f + 0.0001f);
                bool major = (i % 2 == 0) || zeroTick;
                float tickLength = major ? 8f : 4f;

                GUI.color = new Color(1f, 1f, 1f, major ? 0.70f : 0.35f);
                GUI.DrawTexture(new Rect(track.x - tickLength - 2f, tickY - 1f,
                    tickLength, major ? 2f : 1f), whiteTexture);

                if (major && !hideText)
                {
                    string format = dialMax <= 5f ? "0.0" : "0";
                    string label = zeroTick ? "0" : scaleValue.ToString(format);
                    GUI.Label(new Rect(track.x - 48f, tickY - 9f, 36f, 18f),
                        label, dialLabelStyle);
                }
            }

            GUI.color = new Color(1f, 1f, 1f, 0.80f);
            GUI.DrawTexture(new Rect(track.x - 9f, zeroY - 1f, track.width + 18f, 2f), whiteTexture);

            if (haveObservedExtrema)
            {
                float minT = Mathf.InverseLerp(minScale, maxScale,
                    Mathf.Clamp(observedMinG, minScale, maxScale));
                float maxT = Mathf.InverseLerp(minScale, maxScale,
                    Mathf.Clamp(observedMaxG, minScale, maxScale));
                float minY = Mathf.Lerp(track.yMax, track.y, minT);
                float maxY = Mathf.Lerp(track.yMax, track.y, maxT);

                const float extremaMarkerLength = 12f;
                GUI.color = minColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, minY - 2f,
                    extremaMarkerLength, 4f), whiteTexture);
                GUI.color = maxColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, maxY - 2f,
                    extremaMarkerLength, 4f), whiteTexture);
            }

            float clamped = Mathf.Clamp(value, minScale, maxScale);
            float normalized = Mathf.InverseLerp(minScale, maxScale, clamped);
            float markerY = Mathf.Lerp(track.yMax, track.y, normalized);
            GUI.color = GetThresholdColor(value);
            GUI.DrawTexture(new Rect(track.x - 9f, markerY - 3f, track.width + 18f, 6f), whiteTexture);
            GUI.color = old;

            if (!hideText)
                GUI.Label(new Rect(rect.x, rect.yMax - 20f, rect.width, 20f),
                    string.Format("{0:+0.00;-0.00;0.00} G", value), centeredStyle);
        }

        private void DrawHorizontalGVerticalSlider(Rect rect, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), "Horizontal", centeredStyle);

            Color maxColor = new Color(0.25f, 1f, 0.35f, 1f);
            Color minColor = new Color(0.25f, 0.75f, 1f, 1f);
            Color oldTextColor = dialLabelStyle.normal.textColor;

            if (haveObservedHorizontalExtrema)
            {
                dialLabelStyle.normal.textColor = maxColor;
                GUI.Label(new Rect(rect.x, rect.y + 17f, rect.width, 18f),
                    "MAX " + observedMaxHorizontalG.ToString("0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = minColor;
                GUI.Label(new Rect(rect.x, rect.yMax - 38f, rect.width, 18f),
                    "MIN " + observedMinHorizontalG.ToString("0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = oldTextColor;
            }

            float trackTop = rect.y + 38f;
            float trackBottom = rect.yMax - 42f;
            float trackHeight = Mathf.Max(40f, trackBottom - trackTop);
            float trackWidth = Mathf.Min(22f, rect.width * 0.28f);
            float trackX = rect.x + rect.width * 0.62f - trackWidth * 0.5f;
            Rect track = new Rect(trackX, trackTop, trackWidth, trackHeight);

            Color old = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);
            GUI.DrawTexture(track, whiteTexture);

            // Horizontal G is an unsigned magnitude; fill vertically from 0 G at the bottom.
            float fillClamped = Mathf.Clamp(value, 0f, dialMax);
            float fillNormalized = Mathf.InverseLerp(0f, dialMax, fillClamped);
            float fillY = Mathf.Lerp(track.yMax, track.y, fillNormalized);
            if (track.yMax - fillY > 0.5f)
            {
                GUI.color = barFillColor;
                GUI.DrawTexture(new Rect(track.x, fillY, track.width, track.yMax - fillY), whiteTexture);
            }

            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float scaleValue = Mathf.Lerp(0f, dialMax, t);
                float tickY = Mathf.Lerp(track.yMax, track.y, t);
                bool major = (i % 2 == 0);
                float tickLength = major ? 8f : 4f;

                GUI.color = new Color(1f, 1f, 1f, major ? 0.70f : 0.35f);
                GUI.DrawTexture(new Rect(track.x - tickLength - 2f, tickY - 1f,
                    tickLength, major ? 2f : 1f), whiteTexture);

                if (major)
                {
                    string format = dialMax <= 5f ? "0.0" : "0";
                    GUI.Label(new Rect(track.x - 48f, tickY - 9f, 36f, 18f),
                        scaleValue.ToString(format), dialLabelStyle);
                }
            }

            if (haveObservedHorizontalExtrema)
            {
                float minT = Mathf.InverseLerp(0f, dialMax, Mathf.Clamp(observedMinHorizontalG, 0f, dialMax));
                float maxT = Mathf.InverseLerp(0f, dialMax, Mathf.Clamp(observedMaxHorizontalG, 0f, dialMax));
                float minY = Mathf.Lerp(track.yMax, track.y, minT);
                float maxY = Mathf.Lerp(track.yMax, track.y, maxT);

                const float extremaMarkerLength = 12f;
                GUI.color = minColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, minY - 2f, extremaMarkerLength, 4f), whiteTexture);
                GUI.color = maxColor;
                GUI.DrawTexture(new Rect(track.x - extremaMarkerLength - 3f, maxY - 2f, extremaMarkerLength, 4f), whiteTexture);
            }

            float clamped = Mathf.Clamp(value, 0f, dialMax);
            float normalized = Mathf.InverseLerp(0f, dialMax, clamped);
            float markerY = Mathf.Lerp(track.yMax, track.y, normalized);
            GUI.color = GetThresholdColor(value);
            GUI.DrawTexture(new Rect(track.x - 9f, markerY - 3f, track.width + 18f, 6f), whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(rect.x, rect.yMax - 20f, rect.width, 20f),
                string.Format("{0:0.00} G", value), centeredStyle);
        }

        private void DrawHorizontalGSlider(Rect rect, float value)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f),
                string.Format("Horizontal   {0:0.00} G", value), centeredStyle);

            float trackX = rect.x + 16f;
            float trackY = rect.y + 22f;
            float trackWidth = Mathf.Max(40f, rect.width - 32f);
            const float trackHeight = 16f;
            Rect track = new Rect(trackX, trackY, trackWidth, trackHeight);

            Color old = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.08f, 0.90f);
            GUI.DrawTexture(track, whiteTexture);

            // Fill from 0 G at the left edge to the current horizontal-G magnitude.
            float fillClamped = Mathf.Clamp(value, 0f, dialMax);
            float fillNormalized = Mathf.InverseLerp(0f, dialMax, fillClamped);
            float fillX = Mathf.Lerp(track.x, track.xMax, fillNormalized);
            if (fillX - track.x > 0.5f)
            {
                GUI.color = barFillColor;
                GUI.DrawTexture(new Rect(track.x, track.y, fillX - track.x, track.height), whiteTexture);
            }

            // Scale markings: ten intervals, with numeric labels every second tick.
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float tickX = Mathf.Lerp(track.x, track.xMax, t);
                bool major = (i % 2 == 0);
                float extension = major ? 6f : 3f;

                GUI.color = new Color(1f, 1f, 1f, major ? 0.70f : 0.35f);
                // Keep horizontal scale marks short and entirely below the track.
                GUI.DrawTexture(new Rect(tickX - (major ? 1f : 0.5f), track.yMax + 2f,
                    major ? 2f : 1f, extension), whiteTexture);

                if (major)
                {
                    float scaleValue = Mathf.Lerp(0f, dialMax, t);
                    string format = dialMax <= 5f ? "0.0" : "0";
                    GUI.Label(new Rect(tickX - 20f, track.yMax + 5f, 40f, 18f),
                        scaleValue.ToString(format), dialLabelStyle);
                }
            }

            Color maxColor = new Color(0.25f, 1f, 0.35f, 1f);
            Color minColor = new Color(0.25f, 0.75f, 1f, 1f);

            // Observed extrema markers.
            if (haveObservedHorizontalExtrema)
            {
                float minT = Mathf.InverseLerp(0f, dialMax,
                    Mathf.Clamp(observedMinHorizontalG, 0f, dialMax));
                float maxT = Mathf.InverseLerp(0f, dialMax,
                    Mathf.Clamp(observedMaxHorizontalG, 0f, dialMax));
                float minX = Mathf.Lerp(track.x, track.xMax, minT);
                float maxX = Mathf.Lerp(track.x, track.xMax, maxT);

                // Keep extrema markers short and below the horizontal track.
                const float extremaMarkerHeight = 12f;
                GUI.color = minColor;
                GUI.DrawTexture(new Rect(minX - 2f, track.yMax + 3f, 4f, extremaMarkerHeight), whiteTexture);
                GUI.color = maxColor;
                GUI.DrawTexture(new Rect(maxX - 2f, track.yMax + 3f, 4f, extremaMarkerHeight), whiteTexture);
            }

            float clamped = Mathf.Clamp(value, 0f, dialMax);
            float normalized = Mathf.InverseLerp(0f, dialMax, clamped);
            float markerX = Mathf.Lerp(track.x, track.xMax, normalized);
            GUI.color = GetThresholdColor(value);
            GUI.DrawTexture(new Rect(markerX - 3f, track.y - 9f, 6f, track.height + 18f), whiteTexture);
            GUI.color = old;

            if (haveObservedHorizontalExtrema)
            {
                Color oldTextColor = dialLabelStyle.normal.textColor;
                dialLabelStyle.normal.textColor = minColor;
                GUI.Label(new Rect(rect.x, rect.yMax - 18f, rect.width * 0.5f, 18f),
                    "MIN " + observedMinHorizontalG.ToString("0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = maxColor;
                GUI.Label(new Rect(rect.x + rect.width * 0.5f, rect.yMax - 18f, rect.width * 0.5f, 18f),
                    "MAX " + observedMaxHorizontalG.ToString("0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = oldTextColor;
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
                        Vector2 minimumSize = GetMinimumWindowSize();
                        windowRect.width = Mathf.Max(minimumSize.x, resizeStartSize.x + delta.x);
                        windowRect.height = Mathf.Max(minimumSize.y, resizeStartSize.y + delta.y);
                        windowRect.width = Mathf.Min(windowRect.width, Mathf.Max(minimumSize.x, Screen.width - windowRect.x));
                        windowRect.height = Mathf.Min(windowRect.height, Mathf.Max(minimumSize.y, Screen.height - windowRect.y));
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
            if (GUI.Button(new Rect(settingsRect.width - 32, 2, 30, 20), "×", RegisterToolbar.styleXButtonSettings))
            {
                settingsVisible = false;
                SaveSettings();
            }

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

            bool newShowDial = GUILayout.Toggle(showDial, "Show total G display");
            if (newShowDial != showDial)
            {
                showDial = newShowDial;
                SaveSettings();
            }

            GUI.enabled = showDial;
            bool newTotalGAsVerticalGraph = GUILayout.Toggle(totalGAsVerticalGraph,
                "Show total G as vertical graph");
            GUI.enabled = true;
            if (newTotalGAsVerticalGraph != totalGAsVerticalGraph)
            {
                totalGAsVerticalGraph = newTotalGAsVerticalGraph;
                SaveSettings();
            }

            bool newShowVerticalG = GUILayout.Toggle(showVerticalG, "Show vertical G graph");
            if (newShowVerticalG != showVerticalG)
            {
                showVerticalG = newShowVerticalG;
                SaveSettings();
            }

            bool newShowHorizontalG = GUILayout.Toggle(showHorizontalG, "Show horizontal G graph");
            if (newShowHorizontalG != showHorizontalG)
            {
                showHorizontalG = newShowHorizontalG;
                SaveSettings();
            }

            bool newGraphOnlyWhenTotalGOnly = GUILayout.Toggle(graphOnlyWhenTotalGOnly,
                "Hide all text when only Total G is shown");
            if (newGraphOnlyWhenTotalGOnly != graphOnlyWhenTotalGOnly)
            {
                graphOnlyWhenTotalGOnly = newGraphOnlyWhenTotalGOnly;
                SaveSettings();
            }

            GUILayout.Space(6f);

            bool newHideWhenPaused = GUILayout.Toggle(hideWhenPaused, "Hide window when game is paused");
            if (newHideWhenPaused != hideWhenPaused)
            {
                hideWhenPaused = newHideWhenPaused;
                SaveSettings();
            }
            GUILayout.Label("F2 / Hide UI always hides the G-Force Display windows.", HighLogic.Skin.label);

            GUILayout.Space(6f);
            DrawFloatSetting("Dial maximum G", ref dialMaxText);
            DrawFloatSetting("Warning starts at G", ref warningGText);
            DrawFloatSetting("Redline starts at G", ref redlineGText);

            GUILayout.Space(4f);
            GUILayout.Label("Bar fill color (RGB 0-255)", HighLogic.Skin.label);
            GUILayout.BeginHorizontal();
            GUILayout.Label("R", GUILayout.Width(16f));
            barFillRText = GUILayout.TextField(barFillRText, GUILayout.Width(48f));
            GUILayout.Label("G", GUILayout.Width(16f));
            barFillGText = GUILayout.TextField(barFillGText, GUILayout.Width(48f));
            GUILayout.Label("B", GUILayout.Width(16f));
            barFillBText = GUILayout.TextField(barFillBText, GUILayout.Width(48f));
            GUILayout.EndHorizontal();

            ApplySettingsFromText(false);

            GUILayout.Space(4f);
            GUILayout.Label("Warning/redline thresholds use absolute G in signed mode.", HighLogic.Skin.label);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Defaults"))
            {
                dialMax = 10f;
                warningG = 4f;
                redlineG = 6f;
                signedGMode = false;
                showDial = true;
                totalGAsVerticalGraph = false;
                showVerticalG = true;
                showHorizontalG = true;
                graphOnlyWhenTotalGOnly = false;
                hideWhenPaused = true;
                barFillColor = new Color(0.20f, 0.70f, 1.00f, 0.60f);
                ResetSettingText();
                ResetPeaks();
                ResetAccelerationSampling();
                SaveSettings();
            }
            if (GUILayout.Button("Close"))
            {
                settingsVisible = false;
                SaveSettings();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, settingsRect.width, settingsRect.height));
        }

        private void DrawFloatSetting(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(175f));
            value = GUILayout.TextField(value, GUILayout.Width(80f));
            GUILayout.EndHorizontal();
        }

        private void ApplySettingsFromText(bool normalizeText)
        {
            bool changed = false;
            float newDialMax;
            float newWarning;
            float newRedline;

            if (float.TryParse(dialMaxText, out newDialMax))
            {
                newDialMax = Mathf.Clamp(newDialMax, 0.5f, 100f);
                if (!Mathf.Approximately(newDialMax, dialMax))
                {
                    dialMax = newDialMax;
                    changed = true;
                }
            }

            if (float.TryParse(redlineGText, out newRedline))
            {
                newRedline = Mathf.Clamp(newRedline, 0f, dialMax);
                if (!Mathf.Approximately(newRedline, redlineG))
                {
                    redlineG = newRedline;
                    changed = true;
                }
            }

            if (float.TryParse(warningGText, out newWarning))
            {
                newWarning = Mathf.Clamp(newWarning, 0f, redlineG);
                if (!Mathf.Approximately(newWarning, warningG))
                {
                    warningG = newWarning;
                    changed = true;
                }
            }

            // Keep dependent thresholds valid immediately when the dial maximum changes.
            float clampedRedline = Mathf.Clamp(redlineG, 0f, dialMax);
            float clampedWarning = Mathf.Clamp(warningG, 0f, clampedRedline);
            if (!Mathf.Approximately(clampedRedline, redlineG))
            {
                redlineG = clampedRedline;
                changed = true;
            }
            if (!Mathf.Approximately(clampedWarning, warningG))
            {
                warningG = clampedWarning;
                changed = true;
            }

            int r, g, b;
            int currentR = Mathf.RoundToInt(barFillColor.r * 255f);
            int currentG = Mathf.RoundToInt(barFillColor.g * 255f);
            int currentB = Mathf.RoundToInt(barFillColor.b * 255f);

            r = int.TryParse(barFillRText, out r) ? Mathf.Clamp(r, 0, 255) : currentR;
            g = int.TryParse(barFillGText, out g) ? Mathf.Clamp(g, 0, 255) : currentG;
            b = int.TryParse(barFillBText, out b) ? Mathf.Clamp(b, 0, 255) : currentB;

            if (r != currentR || g != currentG || b != currentB)
            {
                barFillColor = new Color(r / 255f, g / 255f, b / 255f, 0.60f);
                changed = true;
            }

            if (normalizeText)
                ResetSettingText();

            if (changed)
                SaveSettings();
        }

        private void ResetSettingText()
        {
            dialMaxText = dialMax.ToString("0.##");
            warningGText = warningG.ToString("0.##");
            redlineGText = redlineG.ToString("0.##");
            barFillRText = Mathf.RoundToInt(barFillColor.r * 255f).ToString();
            barFillGText = Mathf.RoundToInt(barFillColor.g * 255f).ToString();
            barFillBText = Mathf.RoundToInt(barFillColor.b * 255f).ToString();
        }

        private void DrawDial(Rect rect, float g, bool hideText = false)
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

                if (major && !hideText)
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
                DrawExtremaMarker(center, radius, observedMinG, false, hideText);
                DrawExtremaMarker(center, radius, observedMaxG, true, hideText);
            }

            float needleAngle = Mathf.Lerp(-120f, 120f, normalized);
            DrawNeedle(center, radius - 23f, needleAngle, GetThresholdColor(g));

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - 5f, center.y - 5f, 10f, 10f), whiteTexture);
            GUI.color = oldColor;

            if (!hideText)
            {
                Rect valueRect = new Rect(center.x - 65f, center.y + radius * 0.46f, 130f, 28f);
                GUI.Label(valueRect, string.Format("{0:+0.00;-0.00;0.00} G", g), GetValueStyle(g));
            }
        }


        private void DrawExtremaMarker(Vector2 center, float radius, float value, bool isMaximum, bool hideText = false)
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

            if (!hideText)
            {
                Vector2 labelPos = PointOnCircle(center, radius + 23f, angle);
                string prefix = isMaximum ? "MAX " : "MIN ";
                Rect labelRect = new Rect(labelPos.x - 34f, labelPos.y - 9f, 68f, 18f);
                Color oldColor = dialLabelStyle.normal.textColor;
                dialLabelStyle.normal.textColor = markerColor;
                GUI.Label(labelRect, prefix + value.ToString("0.00"), dialLabelStyle);
                dialLabelStyle.normal.textColor = oldColor;
            }
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
            observedMinVerticalG = 0f;
            observedMaxVerticalG = 0f;
            haveObservedVerticalExtrema = false;
            observedMinHorizontalG = 0f;
            observedMaxHorizontalG = 0f;
            haveObservedHorizontalExtrema = false;
        }


        private bool NoDisplaysShown()
        {
            return !showDial && !showVerticalG && !showHorizontalG;
        }

        private bool IsTotalGOnly()
        {
            return showDial && !showVerticalG && !showHorizontalG;
        }

        private bool IsGraphOnlyMode()
        {
            return graphOnlyWhenTotalGOnly && IsTotalGOnly();
        }

        private Vector2 GetMinimumWindowSize()
        {
            if (NoDisplaysShown())
                return new Vector2(EmptyWindowMinWidth, EmptyWindowMinHeight);

            if (IsGraphOnlyMode())
                return new Vector2(GraphOnlyMinWidth, GraphOnlyMinHeight);

            return new Vector2(MinWindowWidth, MinWindowHeight);
        }

        private void NormalizeMainWindowRect()
        {
            Vector2 minimumSize = GetMinimumWindowSize();

            // Protect against corrupt, legacy, NaN/Infinity, or otherwise unusable saved sizes.
            if (float.IsNaN(windowRect.width) || float.IsInfinity(windowRect.width) || windowRect.width < minimumSize.x)
                windowRect.width = Mathf.Max(420f, minimumSize.x);
            if (float.IsNaN(windowRect.height) || float.IsInfinity(windowRect.height) || windowRect.height < minimumSize.y)
                windowRect.height = Mathf.Max(460f, minimumSize.y);

            // Do not allow a restored window to be larger than the usable screen area.
            // On unusually small resolutions, retain as much of the active minimum size as the screen permits.
            float maxWidth = Mathf.Max(120f, Screen.width - 10f);
            float maxHeight = Mathf.Max(120f, Screen.height - 10f);
            windowRect.width = Mathf.Clamp(windowRect.width, Mathf.Min(minimumSize.x, maxWidth), maxWidth);
            windowRect.height = Mathf.Clamp(windowRect.height, Mathf.Min(minimumSize.y, maxHeight), maxHeight);
        }


        private void AlignSettingsWindowToMain()
        {
            // Open Settings immediately to the right of the main window, with top edges aligned.
            settingsRect.x = windowRect.xMax;
            settingsRect.y = windowRect.y;
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
                // Windows always start closed; visibility is session-only.
                windowVisible = false;
                settingsVisible = false;
                signedGMode = config.GetValue<bool>("signedGMode", false);
                showDial = config.GetValue<bool>("showDial", true);
                totalGAsVerticalGraph = config.GetValue<bool>("totalGAsVerticalGraph", false);
                showVerticalG = config.GetValue<bool>("showVerticalG", true);
                showHorizontalG = config.GetValue<bool>("showHorizontalG", true);
                graphOnlyWhenTotalGOnly = config.GetValue<bool>("graphOnlyWhenTotalGOnly", false);
                hideWhenPaused = config.GetValue<bool>("hideWhenPaused", true);
                dialMax = config.GetValue<float>("dialMax", 10f);
                warningG = config.GetValue<float>("warningG", 4f);
                redlineG = config.GetValue<float>("redlineG", 6f);
                float fillR = Mathf.Clamp01(config.GetValue<float>("barFillR", 0.20f));
                float fillG = Mathf.Clamp01(config.GetValue<float>("barFillG", 0.70f));
                float fillB = Mathf.Clamp01(config.GetValue<float>("barFillB", 1.00f));
                barFillColor = new Color(fillR, fillG, fillB, 0.60f);

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
                config.SetValue("showDial", showDial);
                config.SetValue("totalGAsVerticalGraph", totalGAsVerticalGraph);
                config.SetValue("showVerticalG", showVerticalG);
                config.SetValue("showHorizontalG", showHorizontalG);
                config.SetValue("graphOnlyWhenTotalGOnly", graphOnlyWhenTotalGOnly);
                config.SetValue("hideWhenPaused", hideWhenPaused);
                config.SetValue("dialMax", dialMax);
                config.SetValue("warningG", warningG);
                config.SetValue("redlineG", redlineG);
                config.SetValue("barFillR", barFillColor.r);
                config.SetValue("barFillG", barFillColor.g);
                config.SetValue("barFillB", barFillColor.b);
                config.save();
            }
            catch (Exception ex)
            {
                Debug.LogError("[GForceDisplay] Failed to save settings: " + ex);
            }
        }
    }
}
