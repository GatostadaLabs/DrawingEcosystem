using System;
using UnityEngine;

namespace Caronte.DrawingEcosystem
{
    public interface ITemplateDetector
    {
        event Action<DrawingType> TemplateChanged;
        DrawingType CurrentType { get; }
    }

    public interface ITrackedTemplateDetector : ITemplateDetector
    {
        bool HasValidTracking { get; }
        float TrackingStability { get; }
        string TrackingStatus { get; }
        Texture2D CaptureRectifiedFrame();
    }

    public interface ICaptureTrigger
    {
        event Action CaptureRequested;
    }
}
