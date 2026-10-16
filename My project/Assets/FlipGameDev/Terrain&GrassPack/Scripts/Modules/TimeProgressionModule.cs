using UnityEngine;

namespace FlipGameDev.TerrainAndGrassPack.DayNightSystem
{
    public class TimeProgressionModule : MonoBehaviour, IDayNightModule
    {
        public void Initialize(DayNightState state) { }

        public void Tick(DayNightState state, float deltaTime)
        {
            float totalSeconds = state.CycleDurationMinutes * 60f;
            
            // Rule of thirds: 2/3 day, 1/3 night
            float dayDuration = totalSeconds * (2.0f / 3.0f);
            float nightDuration = totalSeconds * (1.0f / 3.0f);

            state.IsDaytime = state.CurrentAngle >= 0f && state.CurrentAngle < 180f;

            float speed = state.IsDaytime 
                ? (180f / dayDuration) 
                : (180f / nightDuration);

            state.CurrentAngle += speed * deltaTime;

            // Keep angle between 0 and 360
            if (state.CurrentAngle >= 360f) 
                state.CurrentAngle -= 360f;
        }

        public void Dispose() { }
    }
}