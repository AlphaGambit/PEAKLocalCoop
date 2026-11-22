using System;
using UnityEngine;

namespace PeakLocalCoop;

internal static class PanelLayout
{
    public static Rect Viewport(int count, int index)
    {
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        switch (count)
        {
            case 1: return new Rect(0f, 0f, 1f, 1f);
            case 2: return new Rect(0f, index == 0 ? 0.5f : 0f, 1f, 0.5f);
            case 3:
                if (index == 0) return new Rect(0f, 0.5f, 1f, 0.5f);
                return new Rect((index - 1) * 0.5f, 0f, 0.5f, 0.5f);
            case 4: return new Rect(index % 2 * 0.5f, index < 2 ? 0.5f : 0f, 0.5f, 0.5f);
            default: throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    public static Rect GuiRect(Rect viewport) =>
        new Rect(viewport.x * Screen.width, (1f - viewport.yMax) * Screen.height,
            viewport.width * Screen.width, viewport.height * Screen.height);
}
