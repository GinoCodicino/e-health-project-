using UnityEngine;

namespace FlipGameDev.TerrainAndGrassPack.DayNightSystem
{
    public class DayNightBrain : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private DayNightState state = new DayNightState();

        private IDayNightModule[] _modules;

        private void Awake()
        {
            _modules = GetComponentsInChildren<IDayNightModule>();

            foreach (var module in _modules)
            {
                module.Initialize(state);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            foreach (var module in _modules)
            {
                module.Tick(state, dt);
            }
        }

        private void OnDestroy()
        {
            if (_modules == null) return;
            foreach (var module in _modules)
            {
                module.Dispose();
            }
        }
    }
}