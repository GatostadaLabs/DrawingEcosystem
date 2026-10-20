using System;

namespace Caronte.DrawingEcosystem
{
    public enum DrawingType
    {
        Mycorrhiza = 0,
        Fungus = 1,
        Bacteria = 2,
        Nematode = 3
    }

    public static class DrawingTypeExtensions
    {
        public static string ToSpanishLabel(this DrawingType type)
        {
            switch (type)
            {
                case DrawingType.Mycorrhiza:
                    return "Micorriza";
                case DrawingType.Fungus:
                    return "Hongo";
                case DrawingType.Bacteria:
                    return "Bacteria";
                case DrawingType.Nematode:
                    return "Nematodo";
                default:
                    return type.ToString();
            }
        }
    }
}
