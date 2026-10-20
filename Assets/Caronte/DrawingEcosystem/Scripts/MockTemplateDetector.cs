using System;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public sealed class MockTemplateDetector : MonoBehaviour, ITemplateDetector
    {
        [SerializeField] private DrawingType currentType;

        public event Action<DrawingType> TemplateChanged;
        public DrawingType CurrentType => currentType;

        public void SelectType(DrawingType type)
        {
            if (currentType == type)
            {
                TemplateChanged?.Invoke(currentType);
                return;
            }

            currentType = type;
            TemplateChanged?.Invoke(currentType);
        }

        public void SelectMycorrhiza() => SelectType(DrawingType.Mycorrhiza);
        public void SelectFungus() => SelectType(DrawingType.Fungus);
        public void SelectBacteria() => SelectType(DrawingType.Bacteria);
        public void SelectNematode() => SelectType(DrawingType.Nematode);
    }
}
