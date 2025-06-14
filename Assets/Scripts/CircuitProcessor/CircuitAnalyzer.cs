using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using System.Linq;
using Sirenix.Utilities;


namespace CircuitProcessor
{
    /// <summary>
    /// Handles circuit analysis using ChatGPT with the XR Circuit Digitizer system prompt
    /// </summary>
    public class CircuitAnalyzer : MonoBehaviour
    {
        [SerializeField] private bool testMode = false;
        [SerializeField, ShowIf("testMode")] private List<TextAsset> testOutputs; // For testing purposes
        private int _testIndex;
        [Header("OpenAI Configuration")]
        [SerializeField, HideIf("testMode")] private OpenAIConfiguration openAIConfiguration;
        private Model chatModel = Model.GPT4o; // Using GPT-4 with vision capabilities

        [Header("System Prompt")]
        [SerializeField, HideIf("testMode")] private TextAsset systemPromptFile; // Drag your markdown file here

        [Header("Debug")]
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;

        private OpenAIClient openAIClient;
        private string systemPrompt;

        // User message template
        private const string USER_MESSAGE_TEMPLATE = @"MODE: ONE-PASS TEST EXECUTION
DOCUMENT: XR Circuit Digitizer Prompt - Canonical Document

Read the document titled above.

Then follow these exact instructions:

- Analyze the attached image using the document as the sole source of truth.
- Complete the full parsing and logic extraction process in a single pass.
- Do not pause, do not ask clarifying questions, do not summarize or list intentions.
- The output must be:
  - One complete JSON object
  - Following the rules, logic, and format from the document
  - Deterministic and reproducible
  - No internal execution fields should ever be included.
- You must respect the defaults, ID naming, conditional logic, and formula structure as specified.
- If errors occur, return the appropriate error JSON as defined.

Now process the image using full one-pass mode and return the output JSON only.";

        void Start()
        {
            if (!testMode)
            {
                InitializeOpenAI();
                LoadSystemPrompt();
            }
        }

        /// <summary>
        /// Initialize the OpenAI client
        /// </summary>
        private void InitializeOpenAI()
        {
            // NOTE: https://github.com/RageAgainstThePixel/com.openai.unity?tab=readme-ov-file#load-key-from-configuration-file
            // NOTE: Regarding this initializing openAI a few very important notes: 
            /**
             *      (1) the recommended implementation does not work in android because local files need to be obtained through URL requests
             *      (2) using a scriptable object OpenAIConfiguration is the way to go. 
                        BUT! you can only place the api-key and nothing else. 
                        The generated default OpenAISettings crash if you add more stuff for unknown reasons.
                        This crashing bug happens for all platforms (not just android)
             **/
            // NOTE: Regardless of your solution please gitignore the file from your repo.
            // NOTE: if you use this go to the Project > Create > OpenAI > OpenAIConfiguration
            /*
            // NOTE: Created .openai and added to Assets/StreamingAssets folder. (as mentioned in:
            // NOTE: Added to .gitignore.            
            string path = Path.Combine(Application.streamingAssetsPath, ".openai");
            openAIClient = new OpenAIClient(new OpenAIAuthentication().LoadFromPath(path));
            */

            openAIClient = new OpenAIClient(openAIConfiguration);
            Debug.Log("OpenAI Client initialized successfully.");
        }

        /// <summary>
        /// Load the system prompt from the markdown file
        /// </summary>
        private void LoadSystemPrompt()
        {
            if (systemPromptFile == null)
            {
                Debug.LogError("System prompt file not assigned! Please assign the markdown file in the inspector.");
                return;
            }

            systemPrompt = systemPromptFile.text;
            Debug.Log($"System prompt loaded. Length: {systemPrompt.Length} characters");
        }

        /// <summary>
        /// Analyze a circuit image and return parsed CircuitData
        /// </summary>
        /// <param name="circuitImage">The circuit image to analyze</param>
        /// <returns>Parsed CircuitData or null if failed</returns>
        public async Task<CircuitData> AnalyzeCircuitAsync(Texture2D circuitImage)
        {
            if (circuitImage == null)
            {
                XRDebugLogViewer.LogError("Circuit image is null!");
                return null;
            }

            try
            {
                XRDebugLogViewer.Log("Starting circuit analysis...", sendToXRDebugLogViewer, sendToDebugLog);
                string jsonResponse;
                if (!testMode)
                {
                    jsonResponse = await PerformChatGPTRequest(circuitImage);
                }
                else
                {
                    jsonResponse = await LoadTestResult();
                }

                if (jsonResponse == null)
                {
                    return null;
                }
                XRDebugLogViewer.Log($"Received response: {jsonResponse}", sendToXRDebugLogViewer, sendToDebugLog);

                // Parse JSON response
                CircuitData circuitData = ParseJsonResponse(jsonResponse);

                if (circuitData != null)
                {
                    XRDebugLogViewer.Log("Circuit analysis completed successfully!", sendToXRDebugLogViewer, sendToDebugLog);
                    LogCircuitData(circuitData);
                }

                return circuitData;
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"Error during circuit analysis: {ex.Message}");
                XRDebugLogViewer.LogError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private async Task<string> LoadTestResult()
        {
            if (testOutputs.IsNullOrEmpty())
            {
                XRDebugLogViewer.LogError($"Circuit analysis - TEST MODE - No test outputs serialized");
                return null;
            }
            await Task.Delay(TimeSpan.FromSeconds(1f));

            string jsonResponse = testOutputs[_testIndex].text;
            _testIndex = (_testIndex + 1) % testOutputs.Count;
            return jsonResponse;
        }

        private async Task<string> PerformChatGPTRequest(Texture2D circuitImage)
        {
            if (openAIClient == null)
            {
                XRDebugLogViewer.LogError("OpenAI client not initialized!");
                return null;
            }

            if (string.IsNullOrEmpty(systemPrompt))
            {
                XRDebugLogViewer.LogError("System prompt not loaded!");
                return null;
            }

            // Convert texture to base64
            //string base64Image = ConvertTextureToBase64(circuitImage);

            // Create chat request
            var chatRequest = new ChatRequest
            (
                new List<Message>
                {
                    new Message(Role.System, systemPrompt),
                    new Message(Role.User, new List<Content>
                    {
                        new Content(ContentType.Text, USER_MESSAGE_TEMPLATE),
                        new Content(circuitImage)
                    })
                },
                model: chatModel,
                //maxTokens: 5000,
                temperature: 0.1f // Low temperature for deterministic output
            );

            // Send request
            var response = await openAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

            if (response?.FirstChoice?.Message?.Content == null)
            {
                XRDebugLogViewer.LogError("No response content received from ChatGPT");
                return null;
            }

            return response.FirstChoice.Message.Content.ToString().Trim();
        }

        /// <summary>
        /// Convert Texture2D to base64 string
        /// </summary>
        /// <param name="texture">Input texture</param>
        /// <returns>Base64 encoded PNG image</returns>
        private string ConvertTextureToBase64(Texture2D texture)
        {
            // Ensure texture is readable
            if (!texture.isReadable)
            {
                XRDebugLogViewer.LogWarning("Texture is not readable. Creating a readable copy...");
                texture = MakeTextureReadable(texture);
            }

            byte[] imageBytes = texture.EncodeToPNG();
            return Convert.ToBase64String(imageBytes);
        }

        /// <summary>
        /// Create a readable copy of a texture
        /// </summary>
        /// <param name="original">Original texture</param>
        /// <returns>Readable texture copy</returns>
        private Texture2D MakeTextureReadable(Texture2D original)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                original.width, original.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            Graphics.Blit(original, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableTexture = new Texture2D(original.width, original.height);
            readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);

            return readableTexture;
        }

        /// <summary>
        /// Parse JSON response into CircuitData
        /// </summary>
        /// <param name="jsonResponse">JSON string from ChatGPT</param>
        /// <returns>Parsed CircuitData or null if failed</returns>
        private CircuitData ParseJsonResponse(string jsonResponse)
        {
            try
            {
                // Clean the JSON response (remove code blocks if present)
                jsonResponse = CleanJsonResponse(jsonResponse);

                // First, try to check if it's an error response
                var tempObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                if (tempObject.ContainsKey("error"))
                {
                    XRDebugLogViewer.LogError($"ChatGPT returned error: {tempObject["error"]}");
                    return null;
                }

                // Parse as CircuitData
                CircuitData circuitData = JsonConvert.DeserializeObject<CircuitData>(jsonResponse);

                // Validate parsed data
                if (circuitData?.components == null)
                {
                    XRDebugLogViewer.LogError("Parsed CircuitData has null components list");
                    return null;
                }

                return circuitData;
            }
            catch (JsonException ex)
            {
                XRDebugLogViewer.LogError($"JSON parsing error: {ex.Message}");
                XRDebugLogViewer.LogError($"JSON content: {jsonResponse}");
                return null;
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"Unexpected error parsing response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clean JSON response by removing markdown code blocks and extra formatting
        /// </summary>
        /// <param name="jsonResponse">Raw response from ChatGPT</param>
        /// <returns>Clean JSON string</returns>
        private string CleanJsonResponse(string jsonResponse)
        {
            // Remove markdown code blocks
            if (jsonResponse.StartsWith("```json"))
            {
                jsonResponse = jsonResponse.Substring(7);
            }
            if (jsonResponse.StartsWith("```"))
            {
                jsonResponse = jsonResponse.Substring(3);
            }
            if (jsonResponse.EndsWith("```"))
            {
                jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
            }

            return jsonResponse.Trim();
        }

        /// <summary>
        /// Log circuit data for debugging
        /// </summary>
        /// <param name="circuitData">Circuit data to log</param>
        private void LogCircuitData(CircuitData circuitData)
        {
            string result = "";
            result += $"Circuit Analysis Results:\n";
            result += $"- Components: {circuitData.components?.Count ?? 0}\n";
            result += $"- Formula: {circuitData.formula}\n";
            result += $"- Verbal Plan: {circuitData.verbalPlan}\n";
            result += $"- Conditional Branches: {circuitData.conditionalBranches?.Count ?? 0}\n";
            result += $"- Notes: {circuitData.notes}\n";
            result += $"- Components: \n";

            if (circuitData.components != null)
            {
                foreach (var component in circuitData.components)
                {
                    result += $"     {component.id} ({component.type}) = {component.Value}\n";
                }
            }
            XRDebugLogViewer.Log($"{result}", sendToXRDebugLogViewer, sendToDebugLog);
        }

        /// <summary>
        /// Public method to analyze circuit from external scripts
        /// </summary>
        /// <param name="imageTexture">Circuit image texture</param>
        /// <param name="callback">Callback with result</param>
        public void AnalyzeCircuit(Texture2D imageTexture, System.Action<CircuitData> callback)
        {
            _ = AnalyzeCircuitWithCallback(imageTexture, callback);
        }

        /// <summary>
        /// Async wrapper for callback-based analysis
        /// </summary>
        private async Task AnalyzeCircuitWithCallback(Texture2D imageTexture, System.Action<CircuitData> callback)
        {
            var result = await AnalyzeCircuitAsync(imageTexture);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Save circuit data to JSON file
        /// </summary>
        /// <param name="circuitData">Circuit data to save</param>
        /// <param name="filename">Output filename</param>
        public void SaveCircuitDataToFile(CircuitData circuitData, string filename = "circuit_analysis.json")
        {
            try
            {
                string json = JsonConvert.SerializeObject(circuitData, Formatting.Indented);
                string filepath = Path.Combine(Application.persistentDataPath, filename);
                File.WriteAllText(filepath, json);
                XRDebugLogViewer.Log($"Circuit data saved to: {filepath}", sendToXRDebugLogViewer, sendToDebugLog);
            }
            catch (Exception ex)
            {
                XRDebugLogViewer.LogError($"Error saving circuit data: {ex.Message}");
            }
        }
    }

}
