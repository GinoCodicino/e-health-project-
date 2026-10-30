using UnityEngine;

namespace FlipGameDev.TerrainAndGrassPack.DayNightSystem
{
    public class CelestialRotationModule : MonoBehaviour, IDayNightModule
    {
        [Header("References")][SerializeField] private Transform rotator;
        [SerializeField] private Transform starsRotator;[Header("Settings")]
        [SerializeField] private float starsSpeed = 0f;

        private float _starsCurrentAngle;

        public void Initialize(DayNightState state)
        {
            if (rotator != null) 
                state.CurrentAngle = rotator.eulerAngles.z;
        }

        public void Tick(DayNightState state, float deltaTime)
        {
            if (rotator != null)
            {
                rotator.localRotation = Quaternion.Euler(0f, 0f, state.CurrentAngle);
            }

            if (starsRotator != null)
            {
                _starsCurrentAngle += starsSpeed * deltaTime;
                starsRotator.localRotation = Quaternion.Euler(0f, 0f, _starsCurrentAngle);
            }
        }

        public void Dispose() { }
    }
}