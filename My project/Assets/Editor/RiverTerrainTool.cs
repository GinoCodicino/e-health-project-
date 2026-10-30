using UnityEngine;
using UnityEditor;

public class RiverTerrainTool : EditorWindow
{
    private Terrain targetTerrain;

    // Parametri Alzamento Fondo
    private float maxDepthThresholdMeters = 10f;
    private float raiseAmountMeters = 2f;
    private bool smoothTransition = true;

    // Parametri Smussamento Bordi (Smooth/Erosione)
    private float smoothMinHeightMeters = 2f;
    private float smoothMaxHeightMeters = 15f;
    private int smoothIterations = 3;
    [Range(1, 5)] private int filterKernelRadius = 2; // Raggio di campionamento per la media
    private bool addNaturalVariation = true;
    [Range(0.01f, 0.5f)] private float variationIntensityMeters = 0.15f;

    [MenuItem("Tools/Terrain/Strumenti Fiume e Argini")]
    public static void ShowWindow()
    {
        GetWindow<RiverTerrainTool>("Fiume Terrain");
    }

    private void OnGUI()
    {
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain Target", targetTerrain, typeof(Terrain), true);

        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }

        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Assegna o seleziona un Terrain attivo nella scena.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(10);
        GUILayout.Label("1. Alza il Letto del Fiume", EditorStyles.boldLabel);
        maxDepthThresholdMeters = EditorGUILayout.FloatField("Soglia Massima Scavo (m)", maxDepthThresholdMeters);
        raiseAmountMeters = EditorGUILayout.FloatField("Altezza da Aggiungere (m)", raiseAmountMeters);
        smoothTransition = EditorGUILayout.Toggle("Transizione Morbida", smoothTransition);

        if (GUILayout.Button("Applica Ripristino Altezza"))
        {
            ApplyRaiseHeight();
        }

        EditorGUILayout.Space(15);
        GUILayout.Label("2. Smussa Argini e Bordi a 90°", EditorStyles.boldLabel);
        smoothMinHeightMeters = EditorGUILayout.FloatField("Quota Minima Argine (m)", smoothMinHeightMeters);
        smoothMaxHeightMeters = EditorGUILayout.FloatField("Quota Massima Argine (m)", smoothMaxHeightMeters);
        smoothIterations = EditorGUILayout.IntSlider("Passaggi di Smussamento", smoothIterations, 1, 10);
        filterKernelRadius = EditorGUILayout.IntSlider("Raggio Campionamento (Pixel)", filterKernelRadius, 1, 4);

        addNaturalVariation = EditorGUILayout.Toggle("Aggiungi Variazione Naturale", addNaturalVariation);
        if (addNaturalVariation)
        {
            variationIntensityMeters = EditorGUILayout.Slider("Intensità Dettaglio Roccia (m)", variationIntensityMeters, 0.01f, 0.5f);
        }

        if (GUILayout.Button("Smussa Bordi Fiume"))
        {
            ApplySmoothBorders();
        }
    }

    private void ApplyRaiseHeight()
    {
        TerrainData tData = targetTerrain.terrainData;
        int resX = tData.heightmapResolution;
        int resY = tData.heightmapResolution;

        Undo.RegisterCompleteObjectUndo(tData, "Alza Letto Fiume");

        float[,] heights = tData.GetHeights(0, 0, resX, resY);
        float tHeight = tData.size.y;
        float normThreshold = Mathf.Clamp01(maxDepthThresholdMeters / tHeight);
        float normRaise = raiseAmountMeters / tHeight;

        for (int y = 0; y < resY; y++)
        {
            for (int x = 0; x < resX; x++)
            {
                float currentH = heights[y, x];
                if (currentH < normThreshold)
                {
                    if (smoothTransition)
                    {
                        float factor = Mathf.SmoothStep(1f, 0f, currentH / normThreshold);
                        heights[y, x] = Mathf.Clamp01(currentH + (normRaise * factor));
                    }
                    else
                    {
                        heights[y, x] = Mathf.Clamp01(currentH + normRaise);
                    }
                }
            }
        }

        tData.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(tData);
    }

    private void ApplySmoothBorders()
    {
        TerrainData tData = targetTerrain.terrainData;
        int resX = tData.heightmapResolution;
        int resY = tData.heightmapResolution;

        Undo.RegisterCompleteObjectUndo(tData, "Smussa Argini Fiume");

        float[,] currentHeights = tData.GetHeights(0, 0, resX, resY);
        float[,] targetHeights = (float[,])currentHeights.Clone();
        float tHeight = tData.size.y;

        float normMin = Mathf.Clamp01(smoothMinHeightMeters / tHeight);
        float normMax = Mathf.Clamp01(smoothMaxHeightMeters / tHeight);
        float normVar = variationIntensityMeters / tHeight;

        // Esegue i passaggi di filtro Box Blur / Media pesata
        for (int pass = 0; pass < smoothIterations; pass++)
        {
            for (int y = 0; y < resY; y++)
            {
                for (int x = 0; x < resX; x++)
                {
                    float centerH = currentHeights[y, x];

                    // Applica lo smoothing solo nella fascia dell'argine (tra il letto e la riva alta)
                    if (centerH >= normMin && centerH <= normMax)
                    {
                        float sum = 0f;
                        int count = 0;

                        for (int ky = -filterKernelRadius; ky <= filterKernelRadius; ky++)
                        {
                            int sampleY = Mathf.Clamp(y + ky, 0, resY - 1);
                            for (int kx = -filterKernelRadius; kx <= filterKernelRadius; kx++)
                            {
                                int sampleX = Mathf.Clamp(x + kx, 0, resX - 1);
                                sum += currentHeights[sampleY, sampleX];
                                count++;
                            }
                        }

                        float smoothedValue = sum / count;

                        // Perlin noise fine per togliere l'effetto "superficie di plastica"
                        if (addNaturalVariation && pass == smoothIterations - 1)
                        {
                            float noise = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * normVar;
                            smoothedValue += noise;
                        }

                        targetHeights[y, x] = Mathf.Clamp01(smoothedValue);
                    }
                }
            }

            // Aggiorna la matrice per il passaggio successivo
            currentHeights = (float[,])targetHeights.Clone();
        }

        tData.SetHeights(0, 0, targetHeights);
        EditorUtility.SetDirty(tData);
    }
}