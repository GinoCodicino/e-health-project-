using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlipGameDev.TerrainAndGrassPack.DayNightSystem
{
    [Serializable]
    public class DayNightState
    {
        [Tooltip("Ângulo atual do ciclo. 0-360")]
        public float CurrentAngle;
        
        [Tooltip("Tempo total do ciclo em minutos.")]
        public float CycleDurationMinutes = 15f;

        /// <summary> Retorna o tempo normalizado entre 0.0 e 1.0 </summary>
        public float NormalizedTime => CurrentAngle / 360f;
        
        /// <summary> Define se atualmente é dia (Ângulo entre 0 e 180) </summary>
        public bool IsDaytime { get; set; }
    }
}