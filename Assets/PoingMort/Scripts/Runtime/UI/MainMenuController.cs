using PoingMort.Controls;
using PoingMort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PoingMort.UI
{
    /// <summary>
    /// Home screen: logo, Continuer (disabled without a save), Nouvelle partie, Options, Crédits, Quitter.
    /// The 3D street and character behind it are part of the MainMenu scene.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        public UITheme theme;
        public TextAsset credits;
        public string versionLabel = "Prototype P01";

        MenuScreen m_Options;
        MenuScreen m_Credits;
        GameObject m_MainColumn;
        Button m_NewGame;
        int m_OpenedFrame;

        UITheme Theme => theme != null ? theme : UITheme.Fallback;

        void Start()
        {
            GameSettings.EnsureLoaded();
            GameInput.Instance.SetContext(InputContext.Menu);
            GameSession.LockCursor(false);
            UIFactory.EnsureEventSystem();
            Build();
        }

        void Build()
        {
            var t = Theme;
            var canvas = UIFactory.CreateCanvas("Interface Accueil", 10, transform);
            var root = (RectTransform)canvas.transform;

            // Soft dark gradient on the left so text stays readable over the 3D scene.
            var shade = UIFactory.Rect("Dégradé", root);
            UIFactory.Place(shade, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900f, 0f));
            var shadeImg = shade.gameObject.AddComponent<Image>();
            shadeImg.sprite = UISprites.VerticalGradient(new Color(0.03f, 0.03f, 0.04f, 0.55f), new Color(0.03f, 0.03f, 0.04f, 0.78f));
            shadeImg.raycastTarget = false;
            var fade = shade.gameObject.AddComponent<HorizontalFade>();
            fade.Apply(shadeImg);

            m_MainColumn = UIFactory.Rect("Colonne", root).gameObject;
            var column = (RectTransform)m_MainColumn.transform;
            UIFactory.Stretch(column);

            if (t.titleSprite != null)
            {
                var logo = UIFactory.Rect("Logo", column);
                UIFactory.Place(logo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(90f, -60f), new Vector2(640f, 640f * t.titleSprite.rect.height / Mathf.Max(1f, t.titleSprite.rect.width)));
                var img = logo.gameObject.AddComponent<Image>();
                img.sprite = t.titleSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            else
            {
                var title = UIFactory.Label(column, "POING MORT", t.Heading, 120, t.text, TextAnchor.UpperLeft, "Titre");
                UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -80f), new Vector2(900f, 160f));
                UIFactory.AddShadow(title, 4f, 0.7f);
            }

            var stack = UIFactory.Rect("Boutons", column);
            UIFactory.Place(stack, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 150f), new Vector2(480f, 360f));
            UIFactory.VerticalStack(stack, 6f, TextAnchor.LowerLeft);

            bool hasSave = SaveSystem.HasSave;
            var cont = UIFactory.MenuButton(stack, t, hasSave ? "Continuer" : "Continuer  <size=20>(aucune sauvegarde)</size>", null);
            cont.interactable = hasSave;
            m_NewGame = UIFactory.MenuButton(stack, t, "Nouvelle partie", SceneFlow.LoadGame);
            var options = UIFactory.MenuButton(stack, t, "Options", () => Open(m_Options));
            var creditsBtn = UIFactory.MenuButton(stack, t, "Crédits", () => Open(m_Credits));
            var quit = UIFactory.MenuButton(stack, t, "Quitter", SceneFlow.Quit);

            var version = UIFactory.Label(column, versionLabel, t.Body, 18, t.textDim, TextAnchor.LowerLeft, "Version");
            UIFactory.Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(112f, 40f), new Vector2(600f, 30f));

            m_Options = MenuScreens.Options(root, t, CloseSubPage);
            m_Credits = MenuScreens.Credits(root, t, credits != null ? credits.text : MenuScreens.DefaultCredits, CloseSubPage);

            MenuScreen.Select(m_NewGame);
        }

        void Open(MenuScreen screen)
        {
            m_Options.Hide();
            m_Credits.Hide();
            screen.Show();
            m_OpenedFrame = Time.frameCount;
        }

        void CloseSubPage()
        {
            m_Options.Hide();
            m_Credits.Hide();
            MenuScreen.Select(m_NewGame);
        }

        void Update()
        {
            var input = GameInput.Instance;
            if (input.MenuBack != null && input.MenuBack.WasPressedThisFrame() && Time.frameCount != m_OpenedFrame)
            {
                if (m_Options.Visible || m_Credits.Visible) CloseSubPage();
            }
        }
    }

    /// <summary>Makes a full-height image fade out toward the right (horizontal alpha ramp via vertex colours).</summary>
    public sealed class HorizontalFade : BaseMeshEffect
    {
        public float startAlpha = 1f;
        public float endAlpha = 0f;

        public void Apply(Graphic graphic)
        {
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = graphic.rectTransform.rect;
            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.InverseLerp(rect.xMin, rect.xMax, v.position.x);
                var c = v.color;
                c.a = (byte)(c.a * Mathf.Lerp(startAlpha, endAlpha, t));
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
