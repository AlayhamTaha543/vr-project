using UnityEngine;

namespace PaintSimulation
{
    public struct PaintDropSpawnData
    {
        public Vector3 SpawnPosition;
        public Vector3 SpawnVelocity;
        public float DropMass;
        public Color PaintColor;
    }

    public interface IBucketPaintSource
    {
        bool CanReleasePaint();
        PaintDropSpawnData GetPaintDropData();
        void NotifyPaintReleased(float massReleased);
    }
}