using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caronte.DrawingEcosystem
{
    public sealed class EcosystemScientificHud : MonoBehaviour
    {
        private static readonly string[] StateLabels =
        {
            "ESTADO CRITICO",
            "DEHESA DEGRADADA",
            "EN RECUPERACION",
            "EQUILIBRIO FUNCIONAL",
            "DEHESA OPTIMA"
        };

        [SerializeField] private EcosystemManager ecosystemManager;
        [SerializeField] private BackgroundVideoController backgroundVideoController;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text mycorrhizaText;
        [SerializeField] private TMP_Text fungusText;
        [SerializeField] private TMP_Text bacteriaText;
        [SerializeField] private TMP_Text nematodeText;
        [SerializeField] private Image scoreFill;
        [SerializeField] private Image activityIndicator;
        [SerializeField] private Color lowStateColor = new Color(1f, 0.29f, 0.2f, 1f);
        [SerializeField] private Color mediumStateColor = new Color(1f, 0.72f, 0.18f, 1f);
        [SerializeField] private Color highStateColor = new Color(0.1f, 0.95f, 0.76f, 1f);

        private EcosystemSnapshot currentSnapshot;
        private int currentBackgroundIndex;

        public void Configure(
            EcosystemManager manager,
            BackgroundVideoController backgroundController,
            TMP_Text state,
            TMP_Text score,
            TMP_Text total,
            TMP_Text mycorrhiza,
            TMP_Text fungus,
            TMP_Text bacteria,
            TMP_Text nematode,
            Image scoreBar,
            Image indicator)
        {
            UnbindEvents();
            ecosystemManager = manager;
            backgroundVideoController = backgroundController;
            stateText = state;
            scoreText = score;
            totalText = total;
            mycorrhizaText = mycorrhiza;
            fungusText = fungus;
            bacteriaText = bacteria;
            nematodeText = nematode;
            scoreFill = scoreBar;
            activityIndicator = indicator;
            BindEvents();
            RefreshAll();
        }

        private void OnEnable()
        {
            BindEvents();
            RefreshAll();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private void Update()
        {
            if (activityIndicator == null)
            {
                return;
            }

            Color color = GetStateColor(currentBackgroundIndex);
            color.a = Mathf.Lerp(0.45f, 1f, (Mathf.Sin(Time.unscaledTime * 3.5f) + 1f) * 0.5f);
            activityIndicator.color = color;
        }

        private void BindEvents()
        {
            if (ecosystemManager != null)
            {
                ecosystemManager.EcosystemChanged -= HandleEcosystemChanged;
                ecosystemManager.EcosystemChanged += HandleEcosystemChanged;
            }

            if (backgroundVideoController != null)
            {
                backgroundVideoController.BackgroundChanged -= HandleBackgroundChanged;
                backgroundVideoController.BackgroundChanged += HandleBackgroundChanged;
            }
        }

        private void UnbindEvents()
        {
            if (ecosystemManager != null)
            {
                ecosystemManager.EcosystemChanged -= HandleEcosystemChanged;
            }

            if (backgroundVideoController != null)
            {
                backgroundVideoController.BackgroundChanged -= HandleBackgroundChanged;
            }
        }

        private void HandleEcosystemChanged(EcosystemSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            RefreshCounters();
            RefreshScore();
        }

        private void HandleBackgroundChanged(int backgroundIndex)
        {
            currentBackgroundIndex = Mathf.Clamp(backgroundIndex, 0, StateLabels.Length - 1);
            RefreshState();
        }

        private void RefreshAll()
        {
            if (ecosystemManager != null)
            {
                currentSnapshot = ecosystemManager.Snapshot;
            }

            currentBackgroundIndex = backgroundVideoController != null
                ? backgroundVideoController.CurrentIndex
                : Mathf.Clamp(currentSnapshot.backgroundIndex, 0, StateLabels.Length - 1);

            RefreshCounters();
            RefreshScore();
            RefreshState();
        }

        private void RefreshCounters()
        {
            SetCount(totalText, currentSnapshot.totalCount);
            SetCount(mycorrhizaText, currentSnapshot.mycorrhizaCount);
            SetCount(fungusText, currentSnapshot.fungusCount);
            SetCount(bacteriaText, currentSnapshot.bacteriaCount);
            SetCount(nematodeText, currentSnapshot.nematodeCount);
        }

        private void RefreshScore()
        {
            float normalizedScore = Mathf.Clamp01(currentSnapshot.score / 100f);
            if (scoreText != null)
            {
                scoreText.text = $"{currentSnapshot.score:00.0}%";
            }

            if (scoreFill != null)
            {
                scoreFill.fillAmount = normalizedScore;
            }
        }

        private void RefreshState()
        {
            Color stateColor = GetStateColor(currentBackgroundIndex);
            if (stateText != null)
            {
                stateText.text = StateLabels[currentBackgroundIndex];
                stateText.color = stateColor;
            }

            if (scoreFill != null)
            {
                scoreFill.color = stateColor;
            }

            if (activityIndicator != null)
            {
                activityIndicator.color = stateColor;
            }
        }

        private Color GetStateColor(int backgroundIndex)
        {
            if (backgroundIndex <= 1)
            {
                return lowStateColor;
            }

            return backgroundIndex == 2 ? mediumStateColor : highStateColor;
        }

        private static void SetCount(TMP_Text target, int count)
        {
            if (target != null)
            {
                target.text = count.ToString("D2");
            }
        }
    }
}
