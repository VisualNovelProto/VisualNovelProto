using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "VN/Character Interaction Library")]
public sealed class CharacterInteractionLibrary : ScriptableObject
{
    [Serializable]
    public struct PoseFrame
    {
        public Vector2 positionOffset;
        public float rotationOffset;
        public float scaleMultiplier;
    }

    [Serializable]
    public struct PoseDefinition
    {
        public string key;
        public PoseFrame initiator;
        public PoseFrame target;
        public float duration;
        public AnimationCurve easing;
        public bool mirrorReturn;
    }

    [SerializeField] PoseDefinition[] poses;

    Dictionary<string, PoseDefinition> cache;

    void OnEnable()
    {
        cache = null;
    }

    void EnsureCache()
    {
        if (cache != null) return;
        cache = new Dictionary<string, PoseDefinition>(StringComparer.OrdinalIgnoreCase);
        if (poses == null) return;
        for (int i = 0; i < poses.Length; i++)
        {
            string key = poses[i].key;
            if (string.IsNullOrWhiteSpace(key)) continue;
            cache[key.Trim()] = poses[i];
        }
    }

    public bool TryGetPose(string key, out PoseDefinition pose)
    {
        EnsureCache();
        if (cache == null)
        {
            pose = default;
            return false;
        }
        return cache.TryGetValue(key?.Trim() ?? string.Empty, out pose);
    }

    public float EvaluatePoseProgress(PoseDefinition pose, float normalizedTime)
    {
        var curve = pose.easing != null && pose.easing.length > 0 ? pose.easing : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        float t = Mathf.Clamp01(normalizedTime);
        if (pose.mirrorReturn)
        {
            if (t <= 0.5f) return curve.Evaluate(t * 2f);
            return curve.Evaluate(2f - t * 2f);
        }
        return curve.Evaluate(t);
    }
}
