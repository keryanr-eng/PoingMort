using UnityEngine;

namespace PoingMort.UI
{
    /// <summary>Fonts, colours and title art of the interface. Created by the scene builder.</summary>
    [CreateAssetMenu(menuName = "PoingMort/Thème d'interface", fileName = "UITheme")]
    public sealed class UITheme : ScriptableObject
    {
        [Header("Polices")]
        public Font bodyFont;
        public Font boldFont;
        [Tooltip("Police condensée des titres de panneaux (le graffiti est réservé au logo).")]
        public Font headingFont;

        [Header("Logo")]
        public Sprite titleSprite;

        [Header("Couleurs")]
        public Color panel = new Color(0.07f, 0.075f, 0.085f, 0.86f);
        public Color panelLight = new Color(0.14f, 0.145f, 0.16f, 0.92f);
        public Color text = new Color(0.93f, 0.92f, 0.9f, 1f);
        public Color textDim = new Color(0.66f, 0.65f, 0.63f, 1f);
        public Color textDisabled = new Color(0.45f, 0.45f, 0.45f, 1f);
        public Color accent = new Color(0.86f, 0.17f, 0.16f, 1f);
        public Color accentDark = new Color(0.5f, 0.09f, 0.09f, 1f);
        public Color health = new Color(0.86f, 0.2f, 0.18f, 1f);
        public Color healthBack = new Color(0.2f, 0.2f, 0.22f, 0.9f);

        static UITheme s_Fallback;

        /// <summary>Theme used when none is assigned (built-in font, default colours).</summary>
        public static UITheme Fallback
        {
            get
            {
                if (s_Fallback == null)
                {
                    s_Fallback = CreateInstance<UITheme>();
                    s_Fallback.name = "UITheme (défaut)";
                }
                return s_Fallback;
            }
        }

        public Font Body => bodyFont != null ? bodyFont : BuiltinFont;
        public Font Bold => boldFont != null ? boldFont : Body;
        public Font Heading => headingFont != null ? headingFont : Bold;

        static Font s_Builtin;

        public static Font BuiltinFont
        {
            get
            {
                if (s_Builtin == null)
                {
                    // Unity 2022.2+ ships "LegacyRuntime.ttf"; older versions used "Arial.ttf".
                    s_Builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (s_Builtin == null) s_Builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return s_Builtin;
            }
        }
    }
}
