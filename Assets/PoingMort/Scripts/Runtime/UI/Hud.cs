using System.Collections.Generic;
using PoingMort.Combat;
using PoingMort.Core;
using PoingMort.Player;
using UnityEngine;
using UnityEngine.UI;

namespace PoingMort.UI
{
    /// <summary>
    /// In-game HUD: health, short objective, contextual interaction, speed (only in a car),
    /// opponent bar and fight status (only during a fight), short notifications.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        public UITheme theme;
        [Tooltip("Petit marqueur discret indiquant qu'il s'agit d'un prototype.")]
        public string prototypeTag = "PROTOTYPE P01";

        PlayerController m_Player;
        FightArena m_Arena;

        GameObject m_Root;
        Image m_HealthFill;
        Image m_HealthLag;
        Text m_Objective;
        GameObject m_PromptGroup;
        Text m_PromptKey;
        Text m_PromptAction;
        Image m_PromptKeyBox;
        Text m_PromptHint;
        GameObject m_Speedo;
        Image m_SpeedFill;
        Text m_SpeedValue;
        GameObject m_FightGroup;
        Text m_OpponentName;
        Image m_OpponentFill;
        Text m_FightStatus;
        readonly List<(Text text, float time)> m_Toasts = new List<(Text, float)>();
        RectTransform m_ToastStack;
        float m_HealthLagValue = 1f;
        float m_StatusPulse;

        UITheme Theme => theme != null ? theme : UITheme.Fallback;

        void Start()
        {
            Build();
            Bind();
        }

        void Bind()
        {
            m_Player = PlayerController.Current;
            m_Arena = FightArena.Current;
            if (m_Player != null) m_Player.Notified += ShowToast;
        }

        void OnDestroy()
        {
            if (m_Player != null) m_Player.Notified -= ShowToast;
        }

        void Build()
        {
            var t = Theme;
            var canvas = UIFactory.CreateCanvas("Interface HUD", 20, transform);
            var root = (RectTransform)canvas.transform;
            m_Root = canvas.gameObject;

            // --- Health (bottom left, as on the combat board)
            var healthBack = UIFactory.Panel(root, "Santé", t.healthBack, 6);
            UIFactory.Place(healthBack.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(380f, 18f));
            m_HealthLag = UIFactory.Panel(healthBack.rectTransform, "Retard", new Color(1f, 1f, 1f, 0.45f), 6);
            m_HealthFill = UIFactory.Panel(healthBack.rectTransform, "Niveau", t.health, 6);
            foreach (var img in new[] { m_HealthLag, m_HealthFill })
                UIFactory.Place(img.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var healthLabel = UIFactory.Label(root, "SANTÉ", t.Heading, 22, t.textDim, TextAnchor.LowerLeft, "Libellé santé");
            UIFactory.Place(healthLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(62f, 82f), new Vector2(300f, 28f));
            UIFactory.AddShadow(healthLabel);

            // --- Objective (top left)
            m_Objective = UIFactory.Label(root, "", t.Body, 24, t.text, TextAnchor.UpperLeft, "Objectif");
            UIFactory.Place(m_Objective.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -48f), new Vector2(760f, 80f));
            UIFactory.AddShadow(m_Objective, 2f, 0.8f);

            // --- Interaction prompt (bottom centre)
            m_PromptGroup = UIFactory.Rect("Interaction", root).gameObject;
            var pg = (RectTransform)m_PromptGroup.transform;
            UIFactory.Place(pg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(700f, 60f));
            var promptBack = UIFactory.Panel(pg, "Fond", t.panel, 10);
            UIFactory.Stretch(promptBack.rectTransform);
            m_PromptKeyBox = UIFactory.Panel(pg, "Touche", t.text, 6);
            UIFactory.Place(m_PromptKeyBox.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(44f, 40f));
            m_PromptKey = UIFactory.Label(m_PromptKeyBox.rectTransform, "F", t.Bold, 24, new Color(0.08f, 0.08f, 0.09f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(m_PromptKey.rectTransform);
            m_PromptAction = UIFactory.Label(pg, "", t.Bold, 26, t.text, TextAnchor.MiddleLeft, "Action");
            UIFactory.Stretch(m_PromptAction.rectTransform, 70f, 16f, 0f, 0f);
            m_PromptHint = UIFactory.Label(root, "", t.Body, 22, t.text, TextAnchor.MiddleCenter, "Indication");
            UIFactory.Place(m_PromptHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(900f, 34f));
            UIFactory.AddShadow(m_PromptHint, 2f, 0.85f);

            // --- Speedometer (bottom right, only in a car)
            m_Speedo = UIFactory.Rect("Compteur", root).gameObject;
            var sp = (RectTransform)m_Speedo.transform;
            UIFactory.Place(sp, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(190f, 190f));
            var disc = UIFactory.Rect("Disque", sp);
            UIFactory.Stretch(disc, 10f, 10f, 10f, 10f);
            var discImg = disc.gameObject.AddComponent<Image>();
            discImg.sprite = UISprites.Circle(128);
            discImg.color = new Color(0.05f, 0.05f, 0.06f, 0.72f);
            var ringBack = UIFactory.Rect("Anneau", sp);
            UIFactory.Stretch(ringBack);
            var ringBackImg = ringBack.gameObject.AddComponent<Image>();
            ringBackImg.sprite = UISprites.Ring(256, 14f);
            ringBackImg.color = new Color(1f, 1f, 1f, 0.16f);
            var ring = UIFactory.Rect("Jauge", sp);
            UIFactory.Stretch(ring);
            m_SpeedFill = ring.gameObject.AddComponent<Image>();
            m_SpeedFill.sprite = UISprites.Ring(256, 14f);
            m_SpeedFill.color = t.accent;
            m_SpeedFill.type = Image.Type.Filled;
            m_SpeedFill.fillMethod = Image.FillMethod.Radial360;
            m_SpeedFill.fillOrigin = (int)Image.Origin360.Bottom;
            m_SpeedFill.fillClockwise = true;
            m_SpeedValue = UIFactory.Label(sp, "0", t.Heading, 72, t.text, TextAnchor.MiddleCenter, "Vitesse");
            UIFactory.Place(m_SpeedValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(0f, 80f));
            var unit = UIFactory.Label(sp, "km/h", t.Body, 22, t.textDim, TextAnchor.MiddleCenter, "Unité");
            UIFactory.Place(unit.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(0f, 30f));

            // --- Fight (top centre, only during a fight)
            m_FightGroup = UIFactory.Rect("Combat", root).gameObject;
            var fg = (RectTransform)m_FightGroup.transform;
            UIFactory.Place(fg, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(620f, 120f));
            m_OpponentName = UIFactory.Label(fg, "", t.Heading, 30, t.text, TextAnchor.UpperCenter, "Nom adversaire");
            UIFactory.Place(m_OpponentName.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 38f));
            UIFactory.AddShadow(m_OpponentName);
            var oppBack = UIFactory.Panel(fg, "Santé adversaire", t.healthBack, 6);
            UIFactory.Place(oppBack.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-60f, 16f));
            m_OpponentFill = UIFactory.Panel(oppBack.rectTransform, "Niveau", t.accent, 6);
            UIFactory.Place(m_OpponentFill.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            m_FightStatus = UIFactory.Label(root, "", t.Heading, 84, t.text, TextAnchor.MiddleCenter, "Statut combat");
            UIFactory.Place(m_FightStatus.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1400f, 120f));
            UIFactory.AddShadow(m_FightStatus, 4f, 0.8f);

            // --- Notifications
            m_ToastStack = UIFactory.Rect("Notifications", root);
            UIFactory.Place(m_ToastStack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(1000f, 160f));
            UIFactory.VerticalStack(m_ToastStack, 4f, TextAnchor.LowerCenter);

            if (!string.IsNullOrEmpty(prototypeTag))
            {
                var tag = UIFactory.Label(root, prototypeTag, t.Body, 16, new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerRight, "Prototype");
                UIFactory.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(400f, 24f));
            }
        }

        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message) || m_ToastStack == null) return;
            foreach (var existing in m_Toasts)
                if (existing.text != null && existing.text.text == message) return;
            var t = Theme;
            var text = UIFactory.Label(m_ToastStack, message, t.Bold, 26, t.text, TextAnchor.MiddleCenter, "Notification");
            text.rectTransform.sizeDelta = new Vector2(1000f, 36f);
            UIFactory.AddLayout(text.gameObject, 1000f, 36f);
            UIFactory.AddShadow(text, 2f, 0.85f);
            m_Toasts.Add((text, Time.unscaledTime));
            if (m_Toasts.Count > 3)
            {
                Destroy(m_Toasts[0].text.gameObject);
                m_Toasts.RemoveAt(0);
            }
        }

        void Update()
        {
            if (m_Player == null) Bind();
            bool paused = GameSession.Current != null && GameSession.Current.IsPaused;
            m_Root.SetActive(!paused);
            if (paused || m_Player == null) return;
            var t = Theme;

            // Health with a short trailing "lag" bar.
            float health = m_Player.Fighter.Health.Normalized;
            m_HealthLagValue = Mathf.MoveTowards(m_HealthLagValue, health, Time.deltaTime * 0.5f);
            if (m_HealthLagValue < health) m_HealthLagValue = health;
            SetFill(m_HealthFill, health);
            SetFill(m_HealthLag, m_HealthLagValue);

            // Objective
            m_Objective.text = ObjectiveText();

            // Prompt
            bool hasPrompt = !string.IsNullOrEmpty(m_Player.PromptAction);
            m_PromptGroup.SetActive(hasPrompt);
            if (hasPrompt)
            {
                m_PromptKey.text = m_Player.PromptKey ?? "";
                m_PromptKeyBox.gameObject.SetActive(!string.IsNullOrEmpty(m_Player.PromptKey));
                m_PromptAction.text = m_Player.PromptAction;
            }
            m_PromptHint.text = m_Player.PromptHint ?? "";

            // Speed only in a car
            var vehicle = m_Player.CurrentVehicle;
            bool inCar = vehicle != null && m_Player.State == PlayerState.EnVoiture;
            m_Speedo.SetActive(inCar);
            if (inCar)
            {
                float kmh = vehicle.Speed * 3.6f;
                m_SpeedValue.text = Mathf.RoundToInt(kmh).ToString();
                float max = vehicle.System != null ? vehicle.System.Parameters.maxSpeed * 3.6f : 80f;
                m_SpeedFill.fillAmount = Mathf.Clamp01(kmh / Mathf.Max(1f, max)) * 0.75f;
            }

            // Fight
            if (m_Arena == null) m_Arena = FightArena.Current;
            bool fight = m_Arena != null && m_Arena.State != FightArena.ArenaState.Waiting;
            m_FightGroup.SetActive(fight);
            if (fight)
            {
                var opp = m_Arena.OpponentFighter;
                m_OpponentName.text = opp != null ? opp.displayName.ToUpperInvariant() : "";
                SetFill(m_OpponentFill, opp != null ? opp.Health.Normalized : 0f);
            }
            string status = m_Arena != null ? m_Arena.StatusText : null;
            m_FightStatus.text = status ?? "";
            if (!string.IsNullOrEmpty(status))
            {
                m_StatusPulse += Time.deltaTime;
                bool win = m_Arena.State == FightArena.ArenaState.Victory;
                bool lose = m_Arena.State == FightArena.ArenaState.Defeat;
                m_FightStatus.color = lose ? t.accent : win ? t.text : Color.Lerp(t.text, t.accent, Mathf.PingPong(m_StatusPulse * 2f, 1f));
                m_FightStatus.fontSize = m_Arena.State == FightArena.ArenaState.Starting ? 96 : 64;
            }
            else m_StatusPulse = 0f;

            // Toast lifetime
            for (int i = m_Toasts.Count - 1; i >= 0; i--)
            {
                var (text, time) = m_Toasts[i];
                float age = Time.unscaledTime - time;
                if (text == null) { m_Toasts.RemoveAt(i); continue; }
                var c = text.color;
                c.a = Mathf.Clamp01(3.2f - age);
                text.color = c;
                if (age > 3.2f)
                {
                    Destroy(text.gameObject);
                    m_Toasts.RemoveAt(i);
                }
            }
        }

        string ObjectiveText()
        {
            if (m_Player.State == PlayerState.KO) return "";
            if (m_Arena != null && m_Arena.State == FightArena.ArenaState.Fighting)
                return "Objectif : mets ton adversaire K.O.";
            if (m_Player.State == PlayerState.EnVoiture)
                return "Objectif : descends en marche quand tu veux (la voiture continue)";
            if (m_Player.BoardingCount == 0)
                return "Objectif : monte dans une voiture en marche";
            if (m_Arena != null && m_Arena.Victories == 0)
                return "Objectif : rejoins la cour et défie l'adversaire";
            return "Quartier libre : roule, combats, recommence";
        }

        static void SetFill(Image img, float value)
        {
            if (img == null) return;
            var rt = img.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            img.enabled = value > 0.001f;
        }
    }
}
