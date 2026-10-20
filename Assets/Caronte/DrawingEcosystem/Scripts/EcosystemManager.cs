using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    [Serializable]
    public struct EcosystemTarget
    {
        public DrawingType type;
        [Range(0f, 1f)] public float targetProportion;
    }

    [Serializable]
    public struct EcosystemSnapshot
    {
        public int totalCount;
        public int mycorrhizaCount;
        public int fungusCount;
        public int bacteriaCount;
        public int nematodeCount;
        public float mycorrhizaProportion;
        public float fungusProportion;
        public float bacteriaProportion;
        public float nematodeProportion;
        public float score;
        public int backgroundIndex;
    }

    public sealed class EcosystemManager : MonoBehaviour
    {
        [SerializeField] private EcosystemTarget[] targetProportions =
        {
            new EcosystemTarget { type = DrawingType.Mycorrhiza, targetProportion = 0.25f },
            new EcosystemTarget { type = DrawingType.Fungus, targetProportion = 0.25f },
            new EcosystemTarget { type = DrawingType.Bacteria, targetProportion = 0.25f },
            new EcosystemTarget { type = DrawingType.Nematode, targetProportion = 0.25f }
        };

        [SerializeField] private float[] backgroundScoreThresholds = { 0f, 25f, 50f, 75f, 90f };

        private readonly Dictionary<DrawingType, int> counters = new Dictionary<DrawingType, int>();
        private EcosystemSnapshot snapshot;

        public event Action<EcosystemSnapshot> EcosystemChanged;
        public EcosystemSnapshot Snapshot => snapshot;

        private void Awake()
        {
            ResetCounters();
        }

        public void ResetCounters()
        {
            counters[DrawingType.Mycorrhiza] = 0;
            counters[DrawingType.Fungus] = 0;
            counters[DrawingType.Bacteria] = 0;
            counters[DrawingType.Nematode] = 0;
            Recalculate();
        }

        public void RegisterSpawn(DrawingType type)
        {
            counters[type] = GetCount(type) + 1;
            Recalculate();
        }

        public void UnregisterSpawn(DrawingType type)
        {
            int currentCount = GetCount(type);
            if (currentCount <= 0)
            {
                return;
            }

            counters[type] = currentCount - 1;
            Recalculate();
        }

        public int GetCount(DrawingType type)
        {
            return counters.TryGetValue(type, out int count) ? count : 0;
        }

        public float GetProportion(DrawingType type)
        {
            int total = snapshot.totalCount;
            return total <= 0 ? 0f : GetCount(type) / (float)total;
        }

        public int GetBackgroundIndexForScore(float score)
        {
            int selected = 0;
            for (int i = 0; i < backgroundScoreThresholds.Length && i < 5; i++)
            {
                if (score >= backgroundScoreThresholds[i])
                {
                    selected = i;
                }
            }

            return Mathf.Clamp(selected, 0, 4);
        }

        public void ForceBackgroundState(int backgroundIndex)
        {
            snapshot.backgroundIndex = Mathf.Clamp(backgroundIndex, 0, 4);
            EcosystemChanged?.Invoke(snapshot);
        }

        private void Recalculate()
        {
            int mycorrhiza = GetCount(DrawingType.Mycorrhiza);
            int fungus = GetCount(DrawingType.Fungus);
            int bacteria = GetCount(DrawingType.Bacteria);
            int nematode = GetCount(DrawingType.Nematode);
            int total = mycorrhiza + fungus + bacteria + nematode;

            float pMycorrhiza = total <= 0 ? 0f : mycorrhiza / (float)total;
            float pFungus = total <= 0 ? 0f : fungus / (float)total;
            float pBacteria = total <= 0 ? 0f : bacteria / (float)total;
            float pNematode = total <= 0 ? 0f : nematode / (float)total;
            float score = total <= 0 ? 0f : CalculateScore(pMycorrhiza, pFungus, pBacteria, pNematode);
            int background = total <= 0 ? 0 : GetBackgroundIndexForScore(score);

            snapshot = new EcosystemSnapshot
            {
                totalCount = total,
                mycorrhizaCount = mycorrhiza,
                fungusCount = fungus,
                bacteriaCount = bacteria,
                nematodeCount = nematode,
                mycorrhizaProportion = pMycorrhiza,
                fungusProportion = pFungus,
                bacteriaProportion = pBacteria,
                nematodeProportion = pNematode,
                score = score,
                backgroundIndex = background
            };

            EcosystemChanged?.Invoke(snapshot);
        }

        private float CalculateScore(float mycorrhiza, float fungus, float bacteria, float nematode)
        {
            float distance = Mathf.Abs(mycorrhiza - GetTarget(DrawingType.Mycorrhiza));
            distance += Mathf.Abs(fungus - GetTarget(DrawingType.Fungus));
            distance += Mathf.Abs(bacteria - GetTarget(DrawingType.Bacteria));
            distance += Mathf.Abs(nematode - GetTarget(DrawingType.Nematode));
            return Mathf.Clamp01(1f - distance / 2f) * 100f;
        }

        private float GetTarget(DrawingType type)
        {
            for (int i = 0; i < targetProportions.Length; i++)
            {
                if (targetProportions[i].type == type)
                {
                    return Mathf.Clamp01(targetProportions[i].targetProportion);
                }
            }

            return 0.25f;
        }
    }
}
