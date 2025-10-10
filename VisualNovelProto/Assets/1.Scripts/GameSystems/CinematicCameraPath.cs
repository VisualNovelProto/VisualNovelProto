using System;
using UnityEngine;

[CreateAssetMenu(menuName = "VN/Cinematic Camera Path")]
public sealed class CinematicCameraPath : ScriptableObject
{
    [Serializable]
    public struct Node
    {
        public Vector2 position;
        public float zoom;
    }

    public bool relative = true;
    public Node[] nodes;
    public AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}
