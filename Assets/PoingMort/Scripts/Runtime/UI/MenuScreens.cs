using System;
using System.Collections.Generic;
using PoingMort.Controls;
using PoingMort.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoingMort.UI
{
    /// <summary>A full-screen page of a menu (options, commandes, crédits...).</summary>
    public sealed class MenuScreen
    {
        public GameObject Root;
        public Selectable FirstSelected;
        public Action OnShow;

        public bool Visible => Root != null && Root.activeSelf;

        public void Show()
        {
            if (Root == null) return;
            Root.SetActive(true);
            OnShow?.Invoke();
            Select(FirstSelected);
        }

        public void Hide()
        {
            if (Root != null) Root.SetActive(false);
        }

        public static void Select(Selectable s)
        {
            if (s == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(s.gameObject);
        }
    }

    /// <summary>Builders for the pages shared by the main menu and the pause menu.</summary>
    public static class MenuScreens
    {
        /// <summary>Dark panel on the right part of the screen, with a heading.</summary>
        public static RectTransform Page(Transform parent, UITheme theme, string title, out RectTransform content, float width = 860f)
        {
            var panel = UIFactory.Panel(parent, "Page " + title, theme.panel, 14);
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            UIFactory.Place(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(width, 860f));

            var heading = UIFactory.Label(rt, title.ToUpperInvariant(), theme.Heading, 52, theme.text, TextAnchor.UpperLeft, "Titre");
            UIFactory.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(50f, -36f), new Vector2(-100f, 70f));
            var line = UIFactory.Panel(rt, "Trait", theme.accent, 0);
            UIFactory.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -110f), new Vector2(90f, 4f));

            content = UIFactory.Rect("Contenu", rt);
            UIFactory.Place(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(50f, -140f), new Vector2(-100f, 620f));
            UIFactory.VerticalStack(content, 10f);
            return rt;
        }

        public static MenuScreen Options(Transform parent, UITheme theme, Action onBack)
        {
            var screen = new MenuScreen();
            var page = Page(parent, theme, "Options", out var content);
            screen.Root = page.gameObject;
            float w = 760f;

            var master = UIFactory.Slider(content, theme, "Volume général", 0f, 1f, GameSettings.MasterVolume, GameSettings.SetMasterVolume, v => Mathf.RoundToInt(v * 100f) + " %", w);
            var effects = UIFactory.Slider(content, theme, "Volume des effets", 0f, 1f, GameSettings.EffectsVolume, GameSettings.SetEffectsVolume, v => Mathf.RoundToInt(v * 100f) + " %", w);
            var sensitivity = UIFactory.Slider(content, theme, "Sensibilité de la caméra", 0.2f, 3f, GameSettings.MouseSensitivity, GameSettings.SetMouseSensitivity, v => v.ToString("0.00"), w);
            var invert = UIFactory.Toggle(content, theme, "Inverser l'axe vertical", GameSettings.InvertY, GameSettings.SetInvertY, w);
            var shake = UIFactory.Slider(content, theme, "Secousses de caméra", 0f, 1f, GameSettings.CameraShake, GameSettings.SetCameraShake, v => v <= 0.001f ? "Aucune" : Mathf.RoundToInt(v * 100f) + " %", w);

            var qualityNames = new List<string>(QualitySettings.names);
            if (qualityNames.Count == 0) qualityNames.Add("Par défaut");
            var quality = UIFactory.Selector(content, theme, "Qualité graphique", qualityNames, GameSettings.CurrentQualityIndex, GameSettings.SetQualityLevel, w);
            var fullscreen = UIFactory.Toggle(content, theme, "Plein écran", GameSettings.Fullscreen, GameSettings.SetFullscreen, w);

            var buttons = UIFactory.Row(content, "Boutons", 70f, w);
            var reset = UIFactory.BoxButton(buttons, theme, "Rétablir par défaut", () =>
            {
                GameSettings.ResetToDefaults();
                master.SetValueWithoutNotify(GameSettings.MasterVolume);
                effects.SetValueWithoutNotify(GameSettings.EffectsVolume);
                sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
                invert.SetIsOnWithoutNotify(GameSettings.InvertY);
                shake.SetValueWithoutNotify(GameSettings.CameraShake);
                fullscreen.SetIsOnWithoutNotify(GameSettings.Fullscreen);
                master.onValueChanged.Invoke(master.value);
                effects.onValueChanged.Invoke(effects.value);
                sensitivity.onValueChanged.Invoke(sensitivity.value);
                shake.onValueChanged.Invoke(shake.value);
            }, 300f, 52f, 22);
            UIFactory.Place((RectTransform)reset.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 52f));
            var back = UIFactory.BoxButton(buttons, theme, "Retour", () => onBack?.Invoke(), 220f, 52f, 22);
            UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(220f, 52f));

            var note = UIFactory.Label(content, "Les réglages sont enregistrés automatiquement sur cet ordinateur.", theme.Body, 20, theme.textDim);
            note.rectTransform.sizeDelta = new Vector2(w, 34f);
            UIFactory.AddLayout(note.gameObject, w, 34f);

            screen.FirstSelected = master;
            screen.OnShow = () =>
            {
                master.SetValueWithoutNotify(GameSettings.MasterVolume);
                effects.SetValueWithoutNotify(GameSettings.EffectsVolume);
                sensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
                invert.SetIsOnWithoutNotify(GameSettings.InvertY);
                shake.SetValueWithoutNotify(GameSettings.CameraShake);
                fullscreen.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            };
            screen.Hide();
            return screen;
        }

        public static MenuScreen Controls(Transform parent, UITheme theme, Action onBack)
        {
            var screen = new MenuScreen();
            var page = Page(parent, theme, "Commandes", out var content);
            screen.Root = page.gameObject;
            var lines = new List<(Text label, Text key, Func<string> keyText)>();
            float w = 760f;
            content.GetComponent<VerticalLayoutGroup>().spacing = 4f;

            void Section(string title)
            {
                var t = UIFactory.Label(content, title.ToUpperInvariant(), theme.Heading, 30, theme.accent);
                t.rectTransform.sizeDelta = new Vector2(w, 42f);
                UIFactory.AddLayout(t.gameObject, w, 42f);
            }

            void Line(string label, Func<string> key)
            {
                var row = UIFactory.Row(content, "Ligne " + label, 32f, w);
                var l = UIFactory.Label(row, label, theme.Body, 24, theme.text);
                UIFactory.Place(l.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                var k = UIFactory.Label(row, key(), theme.Bold, 24, theme.textDim, TextAnchor.MiddleRight);
                UIFactory.Place(k.rectTransform, new Vector2(0.55f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
                lines.Add((l, k, key));
            }

            var input = GameInput.Instance;
            Section("À pied");
            Line("Se déplacer", () => InputPrompts.For(input.Move));
            Line("Sprinter", () => InputPrompts.For(input.Sprint));
            Line("Interagir / monter en voiture", () => InputPrompts.For(input.Interact));
            Line("Coup rapide (enchaînement)", () => InputPrompts.For(input.LightAttack));
            Line("Coup puissant (crochet)", () => InputPrompts.For(input.HeavyAttack));
            Line("Garde (maintenir)", () => InputPrompts.For(input.Guard));
            Line("Esquive", () => InputPrompts.For(input.Dodge));
            Line("Verrouiller la cible", () => InputPrompts.For(input.LockOn));
            Section("En voiture");
            Line("Accélérer / ralentir", () => InputPrompts.For(input.Throttle));
            Line("Changer de voie", () => InputPrompts.For(input.LaneChange));
            Line("Descendre", () => InputPrompts.For(input.ExitVehicle));
            Line("Klaxon", () => InputPrompts.For(input.Horn));
            Line("Pause", () => InputPrompts.For(input.Pause));

            var back = UIFactory.BoxButton(page, theme, "Retour", () => onBack?.Invoke(), 220f, 52f, 22);
            UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(220f, 52f));
            screen.FirstSelected = back;
            screen.OnShow = () =>
            {
                foreach (var line in lines) line.key.text = line.keyText();
            };
            screen.Hide();
            return screen;
        }

        public static MenuScreen Credits(Transform parent, UITheme theme, string creditsText, Action onBack)
        {
            var screen = new MenuScreen();
            var page = Page(parent, theme, "Crédits", out var content);
            screen.Root = page.gameObject;
            var text = UIFactory.Label(content, creditsText, theme.Body, 22, theme.text, TextAnchor.UpperLeft);
            text.rectTransform.sizeDelta = new Vector2(760f, 600f);
            UIFactory.AddLayout(text.gameObject, 760f, 600f);
            var back = UIFactory.BoxButton(page, theme, "Retour", () => onBack?.Invoke(), 220f, 52f, 22);
            UIFactory.Place((RectTransform)back.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-50f, 40f), new Vector2(220f, 52f));
            screen.FirstSelected = back;
            screen.Hide();
            return screen;
        }

        public const string DefaultCredits =
            "<b>POING MORT</b> — prototype P01\n\n" +
            "Conception, code, modèles et animations du prototype : projet Poing Mort.\n\n" +
            "<b>Ressources libres utilisées</b>\n" +
            "• Corps humain de base : MakeHuman / MPFB2 (CC0 1.0)\n" +
            "• Polices : Inter, Bebas Neue, Yusei Magic (SIL Open Font License 1.1)\n" +
            "• Moteur : Unity, Universal Render Pipeline, Input System\n\n" +
            "Textures, sons, voiture, décor, vêtements et animations : créés pour le projet.\n" +
            "Le détail des sources et licences est tenu dans ASSETS.md.";
    }
}
