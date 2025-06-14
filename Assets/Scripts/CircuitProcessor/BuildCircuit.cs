using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace CircuitProcessor
{
    /// <summary>
    /// Orchestrates the circuit building process by coordinating between different circuit processing steps
    /// </summary>
    public class BuildCircuit : MonoBehaviour
    {
        public enum CircuitProductType
        {
            PrefabBased = 0,
            TextBased = 1
        }

        [Header("Circuit Scripts")]
        [SerializeField] private CircuitAnalyzer analyzer;
        [SerializeField] private CircuitGridAssigner gridAssigner;
        [SerializeField] private CircuitASCIIDrawer asciiDrawer;
        [SerializeField] private CircuitASCIIToText asciiToText;
        [SerializeField] private CircuitPrefabDrawer prefabDrawer;

        // FIXME: in the future, for more complex circuits, there won't be a single formula, but multiple
        [Header("Formula Evaluator")]
        [SerializeField] private CircuitFormulaEvaluator formulaEvaluator;


        [Header("Build Settings")]
        [SerializeField] private CircuitProductType finalProduct = CircuitProductType.TextBased;

        [Header("Passthrough Camera Description")]
        [SerializeField] private PassthroughCameraTaker passthroughCameraDescription;

        [Header("Debug Settings")]
        [SerializeField] private bool debugAnalyzer = false;
        [SerializeField] private bool debugGridAssigner = false;
        [SerializeField] private bool debugASCIIDrawer = false;
        [SerializeField] private bool debugASCIIToImage = false;
        [SerializeField] private bool debugPrefabDrawer = false;

        [Header("Output Settings")]
        [SerializeField] private string outputFolder = "CircuitOutputs";
        [SerializeField] private int currentBuildNumber = 0;

        [Header("Debug")]
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;
        private string outputPath;
        private string buildPrefix;
        private CircuitData currentCircuitData;

        private void Awake()
        {
            // Initialize components
            if (analyzer == null)
            {
                Debug.LogError($"[{nameof(BuildCircuit)}] - missing: analyzer");
                return;
            }

            if (gridAssigner == null)
            {
                Debug.LogError($"[{nameof(BuildCircuit)}] - missing: gridAssigner");
                return;
            }

            if (finalProduct == CircuitProductType.TextBased)
            {
                if (asciiDrawer == null)
                {
                    Debug.LogError($"[{nameof(BuildCircuit)}] - missing: asciiDrawer");
                    return;
                }

                if (asciiToText == null)
                {
                    Debug.LogError($"[{nameof(BuildCircuit)}] - missing: asciiToText");
                    return;
                }
            }
            else if (finalProduct == CircuitProductType.PrefabBased)
            {
                if (prefabDrawer == null)
                {
                    Debug.LogError($"[{nameof(BuildCircuit)}] - missing: prefabDrawer");
                    return;
                }
            }

            // Create output directory if it doesn't exist
            outputPath = Path.Combine(Application.persistentDataPath, outputFolder);
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }
        }

        private void Start()
        {
            passthroughCameraDescription.onPictureTaken.AddListener(BeginCircuitBuild);
        }
        
        private void BeginCircuitBuild(Texture2D newImage)
        {
            currentBuildNumber++;
            buildPrefix = $"circuit_{finalProduct}_{currentBuildNumber:D3}";

            analyzer.AnalyzeCircuit(newImage, Build);
        }

        /// <summary>
        /// Builds a circuit from the input JSON data
        /// </summary>
        /// <param name="data">Circuit data to process</param>
        public void Build(CircuitData data)
        {
            try
            {
                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Begin building circuit", sendToXRDebugLogViewer, sendToDebugLog);
                
                if (data == null)
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] ERROR: Input data is null", sendToXRDebugLogViewer, sendToDebugLog);
                    return;
                }

                // Step 0: get chatgpt result debug
                if (debugAnalyzer)
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Saving step 0 debug output", sendToXRDebugLogViewer, sendToDebugLog);
                    SaveDebugOutput(data, $"{buildPrefix}_step0_chatgptoutput.json");
                }

                // Step 1: Process grid positions and generate wires
                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Starting grid assignment", sendToXRDebugLogViewer, sendToDebugLog);
                currentCircuitData = gridAssigner.InitializeGridAssigner(data);
                
                if (currentCircuitData == null)
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] ERROR: Grid assignment failed - returned null", sendToXRDebugLogViewer, sendToDebugLog);
                    return;
                }

                if (debugGridAssigner)
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Saving step 1 debug output", sendToXRDebugLogViewer, sendToDebugLog);
                    SaveDebugOutput(currentCircuitData, $"{buildPrefix}_step1_grid.json");
                }
                
                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Grid Assigner Check", sendToXRDebugLogViewer, sendToDebugLog);

                // Step 2: evaluate formula
                try
                {
                    formulaEvaluator.Initialize(currentCircuitData);
                }
                catch (Exception ex)
                {
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] Formula evaluation failed: {ex.Message}");
                    // Continue with the build process despite formula evaluation failure
                }

                // Step 3: Build final product based on type
                if (finalProduct == CircuitProductType.TextBased)
                {
                    // Convert to ASCII representation
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Starting ASCII conversion", sendToXRDebugLogViewer, sendToDebugLog);
                    currentCircuitData.ascii = asciiDrawer.InitializeDrawASCIICircuit(currentCircuitData);
                    
                    if (currentCircuitData.ascii == null)
                    {
                        XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] ERROR: ASCII conversion failed - returned null", sendToXRDebugLogViewer, sendToDebugLog);
                        return;
                    }

                    if (debugASCIIDrawer)
                    {
                        XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Saving step 2 TEXT debug output", sendToXRDebugLogViewer, sendToDebugLog);
                        SaveDebugOutput(currentCircuitData, $"{buildPrefix}_step2_ascii.json");
                    }
                    
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] ASCII Drawer Check", sendToXRDebugLogViewer, sendToDebugLog);

                    // Update text mesh pro
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Starting ASCII to text conversion", sendToXRDebugLogViewer, sendToDebugLog);
                    asciiToText.InitializeASCIIToText(currentCircuitData);
                    
                    if (debugASCIIToImage)
                    {
                        XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Saving step 3 TEXT debug output", sendToXRDebugLogViewer, sendToDebugLog);
                        SaveDebugOutput(currentCircuitData, $"{buildPrefix}_step3_textmeshpro.json");
                    }
                    
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] ASCII to Text Check", sendToXRDebugLogViewer, sendToDebugLog);

                    // TODO: same logic as in prefab based - formula evaluation
                }
                else // PrefabBased
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Starting prefab-based circuit building", sendToXRDebugLogViewer, sendToDebugLog);
                    prefabDrawer.InitializeDrawPrefabCircuit(currentCircuitData);
                    // FIXME: in the future, I will need to know which formula to assign to each component - not in scope now (we assume all circuits are simple)
                    
                    // assign formula evaluator to thing
                    foreach (var obj in prefabDrawer.InstantiatedObjects)
                    {
                        if (obj.TryGetComponent<CircuitComponentUI>(out var componentUI))
                        {
                            componentUI.AttachFormulaEvaluator(formulaEvaluator);
                        }
                    }
                    if (debugPrefabDrawer)
                    {
                        XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Saving step 2 PREFAB debug output", sendToXRDebugLogViewer, sendToDebugLog);
                        SaveDebugOutput(currentCircuitData, $"{buildPrefix}_step2_prefab.json");
                    }
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Instantiated {prefabDrawer.InstantiatedObjects.Count} objects", sendToXRDebugLogViewer, sendToDebugLog);

                    
                    
                }

                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Circuit build completed successfully", sendToXRDebugLogViewer, sendToDebugLog);
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] CRITICAL ERROR in Build: {ex.Message}");
                XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Saves debug output to JSON file
        /// </summary>
        private void SaveDebugOutput(CircuitData data, string filename)
        {
            try
            {
                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Attempting to save debug output to: {filename}", sendToXRDebugLogViewer, sendToDebugLog);
                
                if (data == null)
                {
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] ERROR: CircuitData is null");
                    return;
                }

                if (string.IsNullOrEmpty(outputPath))
                {
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] ERROR: Output path is not initialized");
                    return;
                }

                string jsonPath = Path.Combine(outputPath, filename);
                XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Full path: {jsonPath}", sendToXRDebugLogViewer, sendToDebugLog);

                // Ensure the directory exists
                if (!Directory.Exists(outputPath))
                {
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Creating output directory: {outputPath}", sendToXRDebugLogViewer, sendToDebugLog);
                    Directory.CreateDirectory(outputPath);
                }

                // Configure JSON serializer settings
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    Converters = new List<JsonConverter>
                    {
                        new Vector2Converter(),
                        new Vector2IntConverter()
                    }
                };

                // Serialize with error handling
                string json;
                try
                {
                    json = JsonConvert.SerializeObject(data, settings);
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Successfully serialized data to JSON", sendToXRDebugLogViewer, sendToDebugLog);
                }
                catch (Exception ex)
                {
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] ERROR: Failed to serialize data: {ex.Message}");
                    return;
                }

                // Write file with error handling
                try
                {
                    File.WriteAllText(jsonPath, json);
                    XRDebugLogViewer.Log($"[{nameof(BuildCircuit)}] Successfully wrote debug output to: {jsonPath}", sendToXRDebugLogViewer, sendToDebugLog);
                }
                catch (Exception ex)
                {
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] ERROR: Failed to write file: {ex.Message}");
                    XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] CRITICAL ERROR in SaveDebugOutput: {ex.Message}");
                XRDebugLogViewer.LogError($"[{nameof(BuildCircuit)}] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Custom JSON converter for Vector2
        /// </summary>
        private class Vector2Converter : JsonConverter<Vector2>
        {
            public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var array = JArray.Load(reader);
                return new Vector2(
                    array[0].Value<float>(),
                    array[1].Value<float>()
                );
            }

            public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue(value.x);
                writer.WriteValue(value.y);
                writer.WriteEndArray();
            }
        }

        /// <summary>
        /// Custom JSON converter for Vector2Int
        /// </summary>
        private class Vector2IntConverter : JsonConverter<Vector2Int>
        {
            public override Vector2Int ReadJson(JsonReader reader, Type objectType, Vector2Int existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var array = JArray.Load(reader);
                return new Vector2Int(
                    array[0].Value<int>(),
                    array[1].Value<int>()
                );
            }

            public override void WriteJson(JsonWriter writer, Vector2Int value, JsonSerializer serializer)
            {
                writer.WriteStartArray();
                writer.WriteValue(value.x);
                writer.WriteValue(value.y);
                writer.WriteEndArray();
            }
        }

        /// <summary>
        /// Cleans up all generated files when the object is destroyed
        /// </summary>
        private void OnDestroy()
        {
            if (Directory.Exists(outputPath))
            {
                try
                {
                    // Delete all files in the output directory
                    string[] files = Directory.GetFiles(outputPath);
                    foreach (string file in files)
                    {
                        File.Delete(file);
                    }
                    Debug.Log($"Cleaned up {files.Length} files from {outputPath}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error cleaning up output files: {e.Message}");
                }
            }
            passthroughCameraDescription.onPictureTaken.RemoveListener(BeginCircuitBuild);
        }
    }

}
