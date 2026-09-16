using Convai.Runtime.Embodiment;
using Convai.Runtime.Components;
using UnityEditor;
using UnityEngine;

namespace Convai.Editor.UI
{
    /// <summary>Shared Convai icon resolver for editor inspectors.</summary>
    internal static class ConvaiBrandedIconProvider
    {
        private static readonly string[] CandidateIconPaths =
        {
            "Packages/com.convai.convai-sdk-for-unity/SDK/Editor/Art/UI/Branding/Convai Icon.png"
        };

        private static Texture2D s_convaiIcon;

        public static Texture2D GetConvaiIcon()
        {
            if (s_convaiIcon != null) return s_convaiIcon;

            if (ConvaiEditorSettings.Instance != null &&
                ConvaiEditorSettings.Instance.ConvaiIconTexture != null)
            {
                s_convaiIcon = ConvaiEditorSettings.Instance.ConvaiIconTexture;
                return s_convaiIcon;
            }

            for (int i = 0; i < CandidateIconPaths.Length; i++)
            {
                s_convaiIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(CandidateIconPaths[i]);
                if (s_convaiIcon != null) return s_convaiIcon;
            }

            s_convaiIcon = EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image as Texture2D;
            if (s_convaiIcon == null)
                s_convaiIcon = Texture2D.whiteTexture;
            return s_convaiIcon;
        }
    }
}

namespace Convai.Editor.Inspectors
{
    /// <summary>Shared branded inspector header for public Convai components.</summary>
    internal static class ConvaiBrandedInspectorChrome
    {
        private static GUIStyle s_titleStyle;
        private static GUIStyle s_subtitleStyle;
        private static GUIStyle s_statusStyle;
        private static Texture2D s_circleTexture;

        public static void DrawHeader(string title, string subtitle = "Convai SDK")
        {
            DrawHeader(title, subtitle, null, Color.clear);
        }

        public static void DrawHeader(string title, string subtitle, string statusText, Color statusColor)
        {
            EnsureStyles();

            const float headerHeight = 46f;
            const float iconSize = 22f;
            const float statusWidth = 128f;

            Rect rowRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true));
            Rect backgroundRect = new(rowRect.x - 18f, rowRect.y - 4f, rowRect.width + 36f, headerHeight + 4f);
            EditorGUI.DrawRect(backgroundRect, ConvaiInspectorThemeTokens.HeaderBackground);

            Texture2D icon = UI.ConvaiBrandedIconProvider.GetConvaiIcon();
            if (icon != null && Event.current.type == EventType.Repaint)
            {
                Rect iconRect = new(rowRect.x, rowRect.y + ((headerHeight - iconSize) * 0.5f), iconSize, iconSize);
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
            }

            float textX = rowRect.x + iconSize + 8f;
            float textWidth = string.IsNullOrWhiteSpace(statusText)
                ? rowRect.xMax - textX
                : Mathf.Max(0f, rowRect.width - iconSize - 8f - statusWidth);
            bool hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
            float titleY = hasSubtitle ? rowRect.y + 8f : rowRect.y + ((headerHeight - 18f) * 0.5f);
            Rect titleRect = new(textX, titleY, textWidth, 18f);
            GUI.Label(titleRect, title, s_titleStyle);

            if (hasSubtitle)
            {
                Rect subtitleRect = new(textX, rowRect.y + 23f, textWidth, 14f);
                GUI.Label(subtitleRect, subtitle, s_subtitleStyle);
            }

            if (!string.IsNullOrWhiteSpace(statusText))
                DrawStatusBadge(new Rect(rowRect.xMax - statusWidth, rowRect.y, statusWidth, rowRect.height),
                    statusText, statusColor);

            GUILayout.Space(6f);
        }

        private static void EnsureStyles()
        {
            if (s_titleStyle != null) return;

            s_titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ConvaiInspectorThemeTokens.AccentEmphasis }
            };

            s_subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.68f, 0.74f, 0.79f, 0.98f) }
            };

            s_statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }

        private static void DrawStatusBadge(Rect rect, string text, Color color)
        {
            if (s_circleTexture == null)
                s_circleTexture = CreateCircleTexture(24);

            const float dotSize = 7f;
            GUIContent content = new(text);
            s_statusStyle.normal.textColor = color;
            Vector2 labelSize = s_statusStyle.CalcSize(content);
            float width = dotSize + 5f + labelSize.x;
            float x = rect.xMax - width - 4f;
            float y = rect.y + ((rect.height - labelSize.y) * 0.5f);
            var dotRect = new Rect(x, rect.y + ((rect.height - dotSize) * 0.5f), dotSize, dotSize);

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(dotRect, s_circleTexture, ScaleMode.ScaleToFit);
            GUI.color = previous;

            GUI.Label(new Rect(dotRect.xMax + 5f, y, labelSize.x, labelSize.y), content, s_statusStyle);
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radiusSq = center * center;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float alpha = (dx * dx) + (dy * dy) <= radiusSq ? 1f : 0f;
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }

    internal static class ConvaiInspectorThemeTokens
    {
        public const int SectionHeaderRowHeight = 22;
        public const int SectionIconFontSize = 16;
        public const int SectionIconCellWidth = 16;
        public const int SectionChevronCellWidth = 12;
        public const int SectionIconSpacing = 1;
        public const int SectionChevronTextSpacing = 2;
        public const int SectionBodyTopPadding = 4;
        public const int SectionBodyBottomPadding = 6;
        public const int SectionBodyBottomFill = 4;
        public const int SectionOuterSpacing = 4;
        public const int SubsectionHeaderRowHeight = 18;
        public const int SubsectionChevronCellWidth = 12;
        public const int SubsectionBodyBottomSpacing = 6;

        public static readonly Color Accent = new(0.322f, 0.718f, 0.533f);
        public static readonly Color AccentEmphasis = new(0.435f, 0.812f, 0.592f);
        public static readonly Color Warning = new(1f, 0.655f, 0.149f);
        public static readonly Color Error = new(0.937f, 0.325f, 0.314f);
        public static readonly Color Info = new(0.129f, 0.588f, 0.953f);
        public static readonly Color HeaderBackground = new(0.18f, 0.18f, 0.18f, 0.9f);

        /// <summary>Muted foreground for subsection headers, so they read as a level below section headers.</summary>
        public static readonly Color SubsectionForeground = new(0.78f, 0.80f, 0.82f, 1f);

        public static readonly Color SectionBackground = new(0.22f, 0.22f, 0.22f, 0.5f);
        public static readonly Color AlternateRowBackground = new(0.2f, 0.2f, 0.2f, 0.3f);
        public static readonly Color TableHeaderBackground = new(0.15f, 0.15f, 0.15f, 0.8f);

        public static Color DividerColor(Color baseColor) => new(baseColor.r, baseColor.g, baseColor.b, 0.3f);
    }

    internal static class ConvaiInspectorStyleCache
    {
        private static bool s_initialized;
        private static bool s_lastProSkin;

        public static GUIStyle SectionHeaderLabelStyle { get; private set; }
        public static GUIStyle SectionIconStyle { get; private set; }
        public static GUIStyle SectionChevronStyle { get; private set; }
        public static GUIStyle SubsectionHeaderLabelStyle { get; private set; }
        public static GUIStyle SubsectionChevronStyle { get; private set; }

        public static void EnsureInitialized()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (s_initialized && s_lastProSkin == proSkin) return;

            s_lastProSkin = proSkin;
            s_initialized = true;

            GUIStyle baseLabel = CreateSafeBaseLabelStyle();
            SectionHeaderLabelStyle = new GUIStyle(baseLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                fixedHeight = ConvaiInspectorThemeTokens.SectionHeaderRowHeight,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0)
            };
            SectionIconStyle = new GUIStyle(SectionHeaderLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = ConvaiInspectorThemeTokens.SectionIconCellWidth,
                fontSize = ConvaiInspectorThemeTokens.SectionIconFontSize,
                contentOffset = Vector2.zero
            };
            SectionChevronStyle = new GUIStyle(SectionHeaderLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = ConvaiInspectorThemeTokens.SectionChevronCellWidth,
                fontSize = 10,
                contentOffset = Vector2.zero
            };
            SubsectionHeaderLabelStyle = new GUIStyle(baseLabel)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                fixedHeight = ConvaiInspectorThemeTokens.SubsectionHeaderRowHeight,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                normal = { textColor = ConvaiInspectorThemeTokens.SubsectionForeground }
            };
            SubsectionChevronStyle = new GUIStyle(SubsectionHeaderLabelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedWidth = ConvaiInspectorThemeTokens.SubsectionChevronCellWidth,
                fontSize = 9,
                contentOffset = Vector2.zero
            };
        }

        private static GUIStyle CreateSafeBaseLabelStyle()
        {
            try
            {
                if (EditorStyles.boldLabel != null)
                    return EditorStyles.boldLabel;
            }
            catch
            {
                // Unity may throw while resolving editor styles in headless batch mode.
            }

            try
            {
                if (GUI.skin?.label != null)
                    return GUI.skin.label;
            }
            catch
            {
                // GUI skin can be unavailable during editor bootstrap.
            }

            return new GUIStyle();
        }
    }

    internal readonly struct ConvaiInspectorSectionHeaderSpec
    {
        public ConvaiInspectorSectionHeaderSpec(
            string editorTypeId,
            string sectionId,
            string title,
            string iconName,
            Color? headerColor = null,
            int iconFontSize = ConvaiInspectorThemeTokens.SectionIconFontSize)
        {
            EditorTypeId = editorTypeId;
            SectionId = sectionId;
            Title = title;
            IconName = iconName;
            HeaderColor = headerColor ?? ConvaiInspectorThemeTokens.Accent;
            IconFontSize = iconFontSize;
        }

        public string EditorTypeId { get; }
        public string SectionId { get; }
        public string Title { get; }
        public string IconName { get; }
        public Color HeaderColor { get; }
        public int IconFontSize { get; }
    }

    internal static class ConvaiInspectorIconIds
    {
        public const string Profile = "symbol:\u2699";
        public const string Discovery = "symbol:\u2315";
        public const string Live = "symbol:\u25cf";
        public const string Validation = "symbol:!";
        public const string Routing = "symbol:\u2194";
        public const string Motion = "symbol:\u25ce";
        public const string Blink = "symbol:\u25c9";
        public const string Range = "symbol:\u25c7";
        public const string Content = "symbol:\u25a3";
        public const string Animator = "symbol:\u25ce";
        public const string Contract = "symbol:\u25c7";
    }

    internal static class ConvaiInspectorSectionChrome
    {
        public static bool DrawHeader(in ConvaiInspectorSectionHeaderSpec spec, bool expanded)
        {
            ConvaiInspectorStyleCache.EnsureInitialized();

            GUIStyle labelStyle = ConvaiInspectorStyleCache.SectionHeaderLabelStyle;
            GUIStyle iconStyle = ConvaiInspectorStyleCache.SectionIconStyle;
            GUIStyle chevronStyle = ConvaiInspectorStyleCache.SectionChevronStyle;
            SetTextColor(labelStyle, spec.HeaderColor);
            SetTextColor(iconStyle, spec.HeaderColor);
            SetTextColor(chevronStyle, spec.HeaderColor);
            iconStyle.fontSize = spec.IconFontSize;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ConvaiInspectorThemeTokens.SectionHeaderRowHeight));
            DrawIcon(spec.IconName, spec.HeaderColor, iconStyle);
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionIconSpacing);
            GUILayout.Label(expanded ? "\u25BC" : "\u25B6", chevronStyle);
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionChevronTextSpacing);

            Rect titleRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                labelStyle,
                GUILayout.Height(ConvaiInspectorThemeTokens.SectionHeaderRowHeight),
                GUILayout.ExpandWidth(true));
            EditorGUI.LabelField(titleRect, spec.Title, labelStyle);
            EditorGUILayout.EndHorizontal();

            Rect headerRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Link);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && headerRect.Contains(current.mousePosition))
            {
                expanded = !expanded;
                current.Use();
            }

            if (expanded)
            {
                Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
                lineRect.x += 4f;
                lineRect.width -= 8f;
                EditorGUI.DrawRect(lineRect, ConvaiInspectorThemeTokens.DividerColor(spec.HeaderColor));
            }

            return expanded;
        }

        /// <summary>
        ///     Draws a collapsible group header one level below <see cref="DrawHeader" />. Use for every group
        ///     inside a section body so nesting reads uniformly instead of mixing labels and foldouts.
        /// </summary>
        public static bool DrawSubsectionHeader(string title, bool expanded)
        {
            ConvaiInspectorStyleCache.EnsureInitialized();

            GUIStyle labelStyle = ConvaiInspectorStyleCache.SubsectionHeaderLabelStyle;
            GUIStyle chevronStyle = ConvaiInspectorStyleCache.SubsectionChevronStyle;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(ConvaiInspectorThemeTokens.SubsectionHeaderRowHeight));
            GUILayout.Space(EditorGUI.indentLevel * 15f);
            GUILayout.Label(expanded ? "▼" : "▶", chevronStyle);
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionChevronTextSpacing);
            GUILayout.Label(title, labelStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            Rect headerRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(headerRect, MouseCursor.Link);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 &&
                headerRect.Contains(current.mousePosition))
            {
                expanded = !expanded;
                current.Use();
            }

            return expanded;
        }

        public static void BeginSubsectionBody()
        {
            GUILayout.Space(2f);
            EditorGUI.indentLevel++;
        }

        public static void EndSubsectionBody()
        {
            EditorGUI.indentLevel--;
            GUILayout.Space(ConvaiInspectorThemeTokens.SubsectionBodyBottomSpacing);
        }

        private static void DrawIcon(string iconName, Color fallbackColor, GUIStyle symbolStyle)
        {
            if (TryGetSymbol(iconName, out string symbol))
            {
                SetTextColor(symbolStyle, fallbackColor);
                Color previous = GUI.color;
                GUI.color = fallbackColor;
                GUILayout.Label(symbol, symbolStyle);
                GUI.color = previous;
                return;
            }

            Rect cellRect = GUILayoutUtility.GetRect(
                ConvaiInspectorThemeTokens.SectionIconCellWidth,
                ConvaiInspectorThemeTokens.SectionHeaderRowHeight,
                GUILayout.Width(ConvaiInspectorThemeTokens.SectionIconCellWidth),
                GUILayout.Height(ConvaiInspectorThemeTokens.SectionHeaderRowHeight));
            Texture image = ResolveIcon(iconName);
            const float iconSize = 16f;
            var iconRect = new Rect(
                cellRect.x + ((cellRect.width - iconSize) * 0.5f),
                cellRect.y + ((cellRect.height - iconSize) * 0.5f),
                iconSize,
                iconSize);

            if (image != null)
            {
                GUI.DrawTexture(iconRect, image, ScaleMode.ScaleToFit, true);
                return;
            }

            var fallbackRect = new Rect(iconRect.x + 4f, iconRect.y + 4f, 8f, 8f);
            EditorGUI.DrawRect(fallbackRect, fallbackColor);
        }

        private static bool TryGetSymbol(string iconName, out string symbol)
        {
            const string prefix = "symbol:";
            if (!string.IsNullOrWhiteSpace(iconName) && iconName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                symbol = iconName.Substring(prefix.Length);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(iconName) && iconName.Length <= 2)
            {
                symbol = iconName;
                return true;
            }

            symbol = null;
            return false;
        }

        private static Texture ResolveIcon(string iconName)
        {
            if (string.IsNullOrWhiteSpace(iconName)) return null;

            string[] candidates = iconName.Split('|');
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i].Trim();
                if (string.IsNullOrEmpty(candidate)) continue;

                GUIContent content = EditorGUIUtility.IconContent(candidate);
                if (content?.image != null)
                    return content.image;
            }

            return null;
        }

        public static void BeginBody(Color? backgroundOverride = null)
        {
            Color background = backgroundOverride ?? ConvaiInspectorThemeTokens.SectionBackground;
            Rect bodyRect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(
                new Rect(
                    bodyRect.x,
                    bodyRect.y,
                    bodyRect.width,
                    bodyRect.height + ConvaiInspectorThemeTokens.SectionBodyBottomFill),
                background);
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionBodyTopPadding);
            EditorGUI.indentLevel++;
        }

        public static void EndBody()
        {
            EditorGUI.indentLevel--;
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionBodyBottomPadding);
            EditorGUILayout.EndVertical();
            GUILayout.Space(ConvaiInspectorThemeTokens.SectionOuterSpacing);
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.onNormal.textColor = color;
            style.focused.textColor = color;
            style.onFocused.textColor = color;
            style.hover.textColor = color;
            style.onHover.textColor = color;
            style.active.textColor = color;
            style.onActive.textColor = color;
        }
    }

    internal static class ConvaiInspectorSectionStateStore
    {
        private const string Prefix = "Convai.Editor";
        private const string Suffix = "Expanded";

        public static bool Get(string hostId, string sectionId, bool defaultValue)
        {
            string key = BuildKey(hostId, sectionId);
            return EditorPrefs.GetBool(key, defaultValue);
        }

        public static void Set(string hostId, string sectionId, bool value)
        {
            string key = BuildKey(hostId, sectionId);
            EditorPrefs.SetBool(key, value);
        }

        internal static string BuildKey(string hostId, string sectionId) =>
            $"{Prefix}.{Normalize(hostId)}.{Normalize(sectionId)}.{Suffix}";

        private static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            return raw.Trim().Replace(" ", string.Empty);
        }
    }

    internal readonly struct ConvaiEmbodimentContextEditorInfo
    {
        public ConvaiEmbodimentContextEditorInfo(
            EmbodimentContext context,
            ConvaiCharacter character,
            Transform runtimeRoot,
            Component inspectedComponent)
        {
            Context = context;
            Character = character;
            RuntimeRoot = runtimeRoot;
            InspectedComponent = inspectedComponent;
        }

        public EmbodimentContext Context { get; }
        public ConvaiCharacter Character { get; }
        public Transform RuntimeRoot { get; }
        public Component InspectedComponent { get; }

        public bool HasExistingContext => Context != null;
        public bool HasConvaiCharacter => Character != null;
        public bool WillAutoCreateOnCharacter => Context == null && Character != null;
        public bool HasSupportedCharacterScope => Context != null || Character != null;

        public string ContextStatus
        {
            get
            {
                if (Context != null) return "Serialized Context";
                if (Character != null) return "Runtime Auto-Resolve";
                return "Local Fallback";
            }
        }

        public string RuntimeRootName => RuntimeRoot != null ? RuntimeRoot.name : "None";
    }

    internal static class ConvaiEmbodimentContextEditorResolver
    {
        public static ConvaiEmbodimentContextEditorInfo Resolve(Component component)
        {
            if (component == null)
                return new ConvaiEmbodimentContextEditorInfo(null, null, null, null);

            EmbodimentContext context = component.GetComponentInParent<EmbodimentContext>(true);
            ConvaiCharacter character = component.GetComponentInParent<ConvaiCharacter>(true);
            Transform runtimeRoot = context != null
                ? context.CharacterRoot
                : character != null
                    ? character.transform
                    : component.transform;

            return new ConvaiEmbodimentContextEditorInfo(context, character, runtimeRoot, component);
        }
    }

    internal abstract class ConvaiPremiumInspectorEditor : UnityEditor.Editor
    {
        protected static readonly Color Accent = ConvaiInspectorThemeTokens.Accent;
        protected static readonly Color AccentEmphasis = ConvaiInspectorThemeTokens.AccentEmphasis;
        protected static readonly Color Warning = ConvaiInspectorThemeTokens.Warning;
        protected static readonly Color Error = ConvaiInspectorThemeTokens.Error;
        protected static readonly Color Info = ConvaiInspectorThemeTokens.Info;
        protected static readonly Color DefaultValueColor = new(0.85f, 0.85f, 0.85f);
        protected static readonly Color IdleColor = new(0.55f, 0.55f, 0.58f);

        protected GUIStyle MiniButtonStyle { get; private set; }
        protected GUIStyle LiveCellLabelStyle { get; private set; }
        protected GUIStyle LiveCellValueStyle { get; private set; }
        private GUIStyle MessageIconStyle { get; set; }
        private GUIStyle MessageTitleStyle { get; set; }
        private GUIStyle MessageBodyStyle { get; set; }

        private bool _stylesReady;

        protected virtual string EditorStateHostId => GetType().Name;

        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
            _stylesReady = false;
        }

        protected void EnsurePremiumStyles()
        {
            if (_stylesReady) return;

            MiniButtonStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.MiniButton))
            {
                fontSize = 10,
                padding = new RectOffset(8, 8, 3, 3)
            };
            LiveCellLabelStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.MiniLabel))
            {
                fontSize = 9,
                normal = { textColor = new Color(0.58f, 0.58f, 0.62f) }
            };
            LiveCellValueStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.Label))
            {
                fontSize = 11,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            MessageIconStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.Label))
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 1, 0)
            };
            MessageTitleStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.BoldLabel))
            {
                fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0)
            };
            MessageBodyStyle = new GUIStyle(CreateSafeStyle(EditorStyleKind.MiniLabel))
            {
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(0, 0, 0, 0)
            };

            _stylesReady = true;
        }

        private enum EditorStyleKind
        {
            Label,
            BoldLabel,
            MiniLabel,
            MiniButton
        }

        private static GUIStyle CreateSafeStyle(EditorStyleKind kind)
        {
            try
            {
                GUIStyle editorStyle = kind switch
                {
                    EditorStyleKind.BoldLabel => EditorStyles.boldLabel,
                    EditorStyleKind.MiniButton => EditorStyles.miniButton,
                    EditorStyleKind.MiniLabel => EditorStyles.miniLabel,
                    _ => EditorStyles.label
                };
                if (editorStyle != null)
                    return editorStyle;
            }
            catch
            {
                // EditorStyles can be unavailable when inspectors are drawn by headless tests.
            }

            try
            {
                GUIStyle skinStyle = kind switch
                {
                    EditorStyleKind.MiniButton => GUI.skin?.button,
                    _ => GUI.skin?.label
                };
                if (skinStyle != null)
                    return skinStyle;
            }
            catch
            {
                // GUI.skin may also be unavailable outside a normal inspector event.
            }

            return new GUIStyle();
        }

        protected void DrawPremiumHeader(string title, string subtitle, string status, Color statusColor)
        {
            EnsurePremiumStyles();
            ConvaiBrandedInspectorChrome.DrawHeader(title, subtitle, status, statusColor);
        }

        protected bool DrawSection(string sectionId, string title, bool expanded, string icon,
            Color? color = null, int? fontSize = null)
        {
            var spec = new ConvaiInspectorSectionHeaderSpec(
                EditorStateHostId,
                sectionId,
                title,
                icon,
                color ?? Accent,
                fontSize ?? ConvaiInspectorThemeTokens.SectionIconFontSize);
            return ConvaiInspectorSectionChrome.DrawHeader(in spec, expanded);
        }

        protected void DrawSectionBody(System.Action draw, Color? background = null)
        {
            ConvaiInspectorSectionChrome.BeginBody(background ?? ConvaiInspectorThemeTokens.SectionBackground);
            draw?.Invoke();
            ConvaiInspectorSectionChrome.EndBody();
        }

        protected void DrawInfoBox(string title, string message) =>
            DrawMessageBox(Info, "i", title, message, null, null);

        protected void DrawWarningBox(string title, string message, string buttonText = null,
            System.Action buttonAction = null) =>
            DrawMessageBox(Warning, "!", title, message, buttonText, buttonAction);

        protected void DrawErrorBox(string title, string message) =>
            DrawMessageBox(Error, "x", title, message, null, null);

        protected void DrawLiveCell(string label, string value, Color valueColor, int width = 104, bool bold = false)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(32), GUILayout.ExpandWidth(false));
            EditorGUILayout.LabelField(label.ToUpperInvariant(), LiveCellLabelStyle, GUILayout.Width(width), GUILayout.Height(14));
            LiveCellValueStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            LiveCellValueStyle.normal.textColor = valueColor;
            EditorGUILayout.LabelField(value, LiveCellValueStyle, GUILayout.Width(width), GUILayout.Height(16));
            EditorGUILayout.EndVertical();
        }

        protected void DrawOfflinePlaceholder(string message = "Enter Play Mode to view live telemetry.")
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color previous = GUI.color;
            GUI.color = IdleColor;
            GUILayout.Label(message, EditorStyles.miniLabel);
            GUI.color = previous;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        protected static string FormatVector3(Vector3 value) =>
            $"{value.x:0.00}, {value.y:0.00}, {value.z:0.00}";

        protected static string ObjectStatus(UnityEngine.Object value) =>
            value != null ? value.name : "Default";

        protected static string RuntimeDependencyStatus(ConvaiEmbodimentContextEditorInfo contextInfo, bool resolved)
        {
            if (resolved) return "Resolved";
            if (contextInfo.HasSupportedCharacterScope) return "Runtime resolved";
            return "Not resolved";
        }

        protected void DrawContextResolutionSummary(
            ConvaiEmbodimentContextEditorInfo contextInfo,
            bool includeInfo,
            string ownerLabel)
        {
            string safeOwnerLabel = string.IsNullOrWhiteSpace(ownerLabel) ? "component" : ownerLabel;
            if (!contextInfo.HasSupportedCharacterScope)
            {
                DrawWarningBox(
                    "Convai Character Missing",
                    $"No ConvaiCharacter ancestor was found. Runtime can create a local context for this {safeOwnerLabel}, " +
                    "but character-scoped behavior routing expects the module to live under a Convai Character hierarchy.");
                return;
            }
        }

        private void DrawMessageBox(Color accentColor, string icon, string title, string body,
            string buttonText, System.Action buttonAction)
        {
            EnsurePremiumStyles();

            const float paddingX = 10f;
            const float paddingY = 8f;
            const float accentWidth = 2f;
            const float iconWidth = 22f;
            const float iconGap = 8f;
            const float titleHeight = 16f;
            const float titleBodyGap = 2f;
            const float buttonHeight = 20f;
            const float buttonTopGap = 6f;
            const float outerTopGap = 10f;
            const float outerBottomGap = 12f;

            float inspectorWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 46f);
            float bodyWidth = Mathf.Max(80f, inspectorWidth - paddingX - iconWidth - iconGap - paddingX);
            float bodyHeight = MessageBodyStyle.CalcHeight(new GUIContent(body), bodyWidth);
            bool hasButton = !string.IsNullOrWhiteSpace(buttonText) && buttonAction != null;
            float contentHeight = titleHeight + titleBodyGap + bodyHeight;
            if (hasButton)
                contentHeight += buttonTopGap + buttonHeight;

            float totalHeight = Mathf.Max(48f, paddingY + contentHeight + paddingY);
            GUILayout.Space(outerTopGap);
            Rect rect = GUILayoutUtility.GetRect(0f, totalHeight, GUILayout.ExpandWidth(true));
            rect.x += 2f;
            rect.width -= 4f;

            Color panelColor = EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.17f, 0.17f, 0.92f)
                : new Color(0.86f, 0.87f, 0.86f, 0.92f);
            Color borderColor = EditorGUIUtility.isProSkin
                ? new Color(accentColor.r, accentColor.g, accentColor.b, 0.32f)
                : new Color(accentColor.r, accentColor.g, accentColor.b, 0.42f);
            Color tintColor = new(accentColor.r, accentColor.g, accentColor.b, EditorGUIUtility.isProSkin ? 0.08f : 0.12f);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, panelColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), borderColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0f, 0f, 0f, 0.18f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, accentWidth, rect.height), accentColor);
                EditorGUI.DrawRect(new Rect(rect.x + accentWidth, rect.y, rect.width - accentWidth, rect.height), tintColor);
            }

            MessageIconStyle.normal.textColor = accentColor;
            MessageTitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.86f, 0.86f)
                : new Color(0.24f, 0.25f, 0.25f);
            MessageBodyStyle.normal.textColor = EditorGUIUtility.isProSkin
                ? new Color(0.68f, 0.70f, 0.70f)
                : new Color(0.34f, 0.35f, 0.35f);

            float contentX = rect.x + paddingX;
            float contentY = rect.y + paddingY;
            Rect iconRect = new(contentX, contentY, iconWidth, titleHeight);
            Rect titleRect = new(iconRect.xMax + iconGap, contentY, bodyWidth, titleHeight);
            Rect bodyRect = new(titleRect.x, titleRect.yMax + titleBodyGap, bodyWidth, bodyHeight);

            GUI.Label(iconRect, icon, MessageIconStyle);
            GUI.Label(titleRect, title, MessageTitleStyle);
            GUI.Label(bodyRect, body, MessageBodyStyle);

            if (!string.IsNullOrWhiteSpace(buttonText) && buttonAction != null)
            {
                Rect buttonRect = new(
                    titleRect.x,
                    bodyRect.yMax + buttonTopGap,
                    Mathf.Min(132f, bodyWidth),
                    buttonHeight);
                if (GUI.Button(buttonRect, buttonText, MiniButtonStyle))
                    buttonAction();
            }

            GUILayout.Space(outerBottomGap);
        }
    }
}
