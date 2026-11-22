using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace PeakLocalCoop;

internal sealed class FrameSampler
{
    private readonly ManualLogSource log;
    private readonly string destination;
    private readonly FrameTiming[] timing = new FrameTiming[1];

    public FrameSampler(ManualLogSource logger, string destination)
    {
        log = logger;
        this.destination = destination;
        File.WriteAllText(destination, "utc,players,scenario,scene,width,height,focused,frame_ms,gpu_ms\n");
    }

    public IEnumerator Sample(int count, string scenario, float seconds)
    {
        var frameTimes = new List<double>(Mathf.CeilToInt(seconds * 144f));
        var gpuTimes = new List<double>(frameTimes.Capacity);
        var started = Time.realtimeSinceStartupAsDouble;
        var unfocused = 0;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        using (var writer = File.AppendText(destination))
        {
            while (Time.realtimeSinceStartupAsDouble - started < seconds)
            {
                yield return null;
                var duration = Time.unscaledDeltaTime * 1000d;
                var focused = Application.isFocused;
                if (!focused) unfocused++;
                FrameTimingManager.CaptureFrameTimings();
                var available = FrameTimingManager.GetLatestTimings(1, timing) == 1 && timing[0].gpuFrameTime > 0d;
                frameTimes.Add(duration);
                if (available) gpuTimes.Add(timing[0].gpuFrameTime);
                var gpuCell = available ? timing[0].gpuFrameTime.ToString("F3", CultureInfo.InvariantCulture) : "";
                writer.WriteLine(FormattableString.Invariant(
                    $"{DateTime.UtcNow:O},{count},{scenario},{scene},{Screen.width},{Screen.height},{(focused ? 1 : 0)},{duration:F3},{gpuCell}"));
            }
        }

        if (frameTimes.Count == 0) yield break;
        frameTimes.Sort();
        var mean = frameTimes.Average();
        var p99 = frameTimes[Mathf.Clamp(Mathf.CeilToInt(frameTimes.Count * 0.99f) - 1, 0, frameTimes.Count - 1)];
        var gpu = gpuTimes.Count > 0 ? gpuTimes.Average().ToString("F2", CultureInfo.InvariantCulture) : "unavailable";
        log.LogInfo(FormattableString.Invariant(
            $"BENCH {count} {scenario} {scene} {Screen.width}x{Screen.height}: {frameTimes.Count} frames, {1000d / mean:F2} FPS, mean {mean:F2} ms, p99 {p99:F2} ms, mean GPU {gpu} ms, {unfocused} unfocused."));
    }
}
