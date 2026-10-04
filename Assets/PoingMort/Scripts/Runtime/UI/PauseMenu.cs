using PoingMort.Controls;
using PoingMort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PoingMort.UI
{
    /// <summary>Pause (Échap): Reprendre, Options, Commandes, Retour à l'accueil.</summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public UITheme theme;

        GameObject m_Root;
        MenuScreen m_Options;
        MenuScreen m_Controls;
        Button m_Resume;
        int m_OpenedFrame;

        UITheme Theme => theme != null ? theme : UITheme.Fallback;

        void Start()
        {
            UIFactory.EnsureEventSystem();
            Build();
            m_Root.SetActive(false);
            if (GameSession.Current != null) GameSession.Current.PauseChanged += OnPauseChanged;
        }

        void OnDestroy()
        {
            if (GameSession.Current != null) GameSession.Current.PauseChanged -= OnPauseChanged;
        }

        void Build()
        {
            var t = Theme;
            var canvas = UIFactory.CreateCanvas("Interface Pause", 50, transform);
            var root = (RectTransform)canvas.transform;
            m_Root = canvas.gameObject;

            var overlay = UIFactory.Panel(root, "Voile", new Color(0.02f, 0.02f, 0.03f, 0.62f), 0);
            UIFactory.Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            var title = UIFactory.Label(root, "PAUSE", t.Heading, 96, t.text, TextAnchor.UpperLeft, "Titre");
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -110f), new Vector2(600f, 120f));
            var line = UIFactory.Panel(root, "Trait", t.accent, 0);
            UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(114f, -228f), new Vector2(120f, 5f));

            var stack = UIFactory.Rect("Boutons", root);
            UIFactory.Place(stack, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, -40f), new Vector2(520f, 300f));
            UIFactory.VerticalStack(stack, 6f, TextAnchor.UpperLeft);
            m_Resume = UIFactory.MenuButton(stack, t, "Reprendre", Resume);
            UIFactory.MenuButton(stack, t, "Options", () => Open(m_Options));
            UIFactory.MenuButton(stack, t, "Commandes", () => Open(m_Controls));
            UIFactory.MenuButton(stack, t, "Retour à l'accueil", () =>
            {
                if (GameSession.Current != null) GameSession.Current.ReturnToMainMenu();
                else SceneFlow.LoadMainMenu();
            });

            m_Options = MenuScreens.Options(root, t, CloseSubPage);
            m_Controls = MenuScreens.Controls(root, t, CloseSubPage);
        }

        void OnPauseChanged(bool paused)
        {
            if (m_Root == null) return;
            m_Root.SetActive(paused);
            m_OpenedFrame = Time.frameCount;
            if (paused)
            {
                m_Options.Hide();
                m_Controls.Hide();
                MenuScreen.Select(m_Resume);
            }
        }

        void Resume()
        {
            if (GameSession.Current != null) GameSession.Current.SetPaused(false);
        }

        void Open(MenuScreen screen)
        {
            m_Options.Hide();
            m_Controls.Hide();
            screen.Show();
            m_OpenedFrame = Time.frameCount;
        }

        void CloseSubPage()
        {
            m_Options.Hide();
            m_Controls.Hide();
            MenuScreen.Select(m_Resume);
        }

        void Update()
        {
            if (m_Root == null || !m_Root.activeSelf) return;
            var input = GameInput.Instance;
            if (input.MenuBack == null || !input.MenuBack.WasPressedThisFrame() || Time.frameCount == m_OpenedFrame) return;
            if (m_Options.Visible || m_Controls.Visible) CloseSubPage();
            else Resume();
        }
    }
}
