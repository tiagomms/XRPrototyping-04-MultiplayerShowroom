using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CircuitProcessor;

namespace CircuitProcessor
{

    /// <summary>
    /// Handles the conversion of circuit data into 3D prefab representation
    /// </summary>
    public class CircuitPrefabDrawer : MonoBehaviour
    {
        [SerializeField] private GameObject circuitTable;
        // Fixed layout parameters from specification
        [SerializeField] private int WIRE_LENGTH = 5;
        [SerializeField] private int HORIZONTAL_PADDING = 0;
        [SerializeField] private int VERTICAL_PADDING = 0;

        [Header("Parent Transform")]
        [SerializeField] private Transform parentTransform;

        [Header("Component Prefabs")]
        [SerializeField] private GameObject resistorPrefab;
        [SerializeField] private GameObject lightbulbPrefab;
        [SerializeField] private GameObject batteryPrefab;
        [SerializeField] private GameObject switchPrefab;
        [SerializeField] private GameObject forkPrefab;
        [SerializeField] private GameObject mergePrefab;

        [Header("Wire Prefab")]
        [SerializeField] private GameObject wirePrefab;

        [Header("Wire Offset Settings")]
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfResistor = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfLightbulb = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfBattery = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfSwitch = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfFork = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfMerge = 0f;
        [SerializeField, Range(-0.5f, 0.5f)] private float wireOffsetIfWire = 0f;

        [Header("Debug")]
        [SerializeField] private bool sendToXRDebugLogViewer = true;
        [SerializeField] private bool sendToDebugLog = true;

        // Public list to store instantiated objects
        public List<GameObject> InstantiatedObjects { get; private set; } = new List<GameObject>();

        private void Awake()
        {
            // Check for required prefabs
            if (resistorPrefab == null)
                Debug.LogError("ResistorPrefab is not assigned in CircuitPrefabDrawer!");
            if (lightbulbPrefab == null)
                Debug.LogError("LightbulbPrefab is not assigned in CircuitPrefabDrawer!");
            if (batteryPrefab == null)
                Debug.LogError("BatteryPrefab is not assigned in CircuitPrefabDrawer!");
            if (switchPrefab == null)
                Debug.LogError("SwitchPrefab is not assigned in CircuitPrefabDrawer!");
            if (wirePrefab == null)
                Debug.LogError("WirePrefab is not assigned in CircuitPrefabDrawer!");
            if (parentTransform == null)
                Debug.LogError("ParentTransform is not assigned in CircuitPrefabDrawer!");
        }

        private void Start()
        {
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Starting initialization", sendToXRDebugLogViewer, sendToDebugLog);
            circuitTable.SetActive(false);
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Circuit table deactivated", sendToXRDebugLogViewer, sendToDebugLog);
        }

        /// <summary>
        /// Clears all previously instantiated objects
        /// </summary>
        public void ClearInstantiatedObjects()
        {
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Clearing {InstantiatedObjects.Count} objects", sendToXRDebugLogViewer, sendToDebugLog);
            foreach (var obj in InstantiatedObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            InstantiatedObjects.Clear();
            XRDebugLogViewer.Log("CircuitPrefabDrawer: All objects cleared", sendToXRDebugLogViewer, sendToDebugLog);
        }

        /// <summary>
        /// Places the complete circuit using prefabs
        /// </summary>
        public void InitializeDrawPrefabCircuit(CircuitData circuitData)
        {
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Starting circuit initialization", sendToXRDebugLogViewer, sendToDebugLog);
            circuitTable.SetActive(true);
            
            // Clear previous objects before creating new ones
            ClearInstantiatedObjects();

            // Initialize positions (reuse logic from ASCII drawer)
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Initializing positions", sendToXRDebugLogViewer, sendToDebugLog);
            InitializePositions(circuitData);

            // Place all wires first
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Placing {circuitData.wires.Count} wires", sendToXRDebugLogViewer, sendToDebugLog);
            foreach (var wire in circuitData.wires)
            {
                var wireObject = PlaceWire(circuitData, wire);
                if (wireObject != null)
                    InstantiatedObjects.Add(wireObject);
            }

            // Place all components
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Placing {circuitData.components.Count} components", sendToXRDebugLogViewer, sendToDebugLog);
            foreach (var component in circuitData.components)
            {
                var componentObject = PlaceComponent(circuitData, component);
                if (componentObject != null)
                    InstantiatedObjects.Add(componentObject);
            }
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Circuit initialization complete", sendToXRDebugLogViewer, sendToDebugLog);
        }

        /// <summary>
        /// Calculates ASCII position based on grid position with padding
        /// </summary>
        private Vector2Int CalculateAsciiPosition(Vector2Int gridPosition)
        {
            var result = new Vector2Int(
                gridPosition.x * WIRE_LENGTH + HORIZONTAL_PADDING,
                gridPosition.y * WIRE_LENGTH + VERTICAL_PADDING
            );
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Calculated ASCII position {result} from grid position {gridPosition}", sendToXRDebugLogViewer, sendToDebugLog);
            return result;
        }

        /// <summary>
        /// Initializes positions for all components and wires
        /// </summary>
        private void InitializePositions(CircuitData circuitData)
        {
            // Calculate ASCII positions for all components and wires
            foreach (var component in circuitData.components)
            {
                component.asciiPosition = CalculateAsciiPosition(component.gridPosition);
            }

            foreach (var wire in circuitData.wires)
            {
                wire.fromASCII = CalculateAsciiPosition(wire.fromGrid);
                wire.toASCII = CalculateAsciiPosition(wire.toGrid);
            }
        }

        /// <summary>
        /// Places a wire prefab in the scene
        /// </summary>
        private GameObject PlaceWire(CircuitData circuitData, Wire wire)
        {
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Attempting to place wire from {wire.fromASCII} to {wire.toASCII}", sendToXRDebugLogViewer, sendToDebugLog);
            if (wirePrefab == null)
            {
                XRDebugLogViewer.LogError("CircuitPrefabDrawer: ERROR - Wire prefab is null");
                return null;
            }

            // Instantiate wire
            GameObject wireObject = Instantiate(wirePrefab, parentTransform);
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Wire instantiated at {wireObject.transform.position}", sendToXRDebugLogViewer, sendToDebugLog);
            
            // Calculate initial position: (fromASCII.y, 0, fromASCII.x)
            wireObject.transform.localPosition = new Vector3(wire.fromASCII.y, 0, wire.fromASCII.x);
            
            // Calculate rotation based on wire direction
            float yRotation = 0f;
            int deltaY = wire.toASCII.y - wire.fromASCII.y;
            
            if (deltaY == 0) // Horizontal
                yRotation = 0f;
            else if (deltaY > 0) // Vertical positive
                yRotation = 90f;
            else // Vertical negative
                yRotation = -90f;

            wireObject.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Wire rotation set to {yRotation} degrees", sendToXRDebugLogViewer, sendToDebugLog);

            // Calculate initial scale based on distance
            Vector3 scale = wireObject.transform.localScale;
            if (deltaY == 0) // Horizontal wire
            {
                scale.z = Mathf.Abs(wire.toASCII.x - wire.fromASCII.x);
            }
            else // Vertical wire
            {
                scale.z = Mathf.Abs(wire.toASCII.y - wire.fromASCII.y);
            }
            wireObject.transform.localScale = scale;
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Wire scale set to {scale}", sendToXRDebugLogViewer, sendToDebugLog);

            // Add WireData component
            WireData wireData = wireObject.GetComponent<WireData>();
            if (wireData == null)
            {
                XRDebugLogViewer.LogError("CircuitPrefabDrawer: ERROR - WireData component not found on wire prefab");
                return null;
            }
            wireData.Initialize(wire);

            // Apply wire offsets and scaling
            ApplyWireOffsets(circuitData, wire, wireObject);
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Wire placement complete", sendToXRDebugLogViewer, sendToDebugLog);

            return wireObject;
        }

        /// <summary>
        /// Applies offset logic to wire prefabs
        /// </summary>
        private void ApplyWireOffsets(CircuitData circuitData, Wire wire, GameObject wireObject)
        {
            Vector3 position = wireObject.transform.localPosition;
            Vector3 scale = wireObject.transform.localScale;
            
            // Apply component-based offsets
            if (wire.startTouchesComponent)
            {
                var startComponent = FindComponentAtPosition(circuitData, wire.fromASCII);
                if (startComponent != null)
                {
                    float offset = GetWireOffsetForComponentType(startComponent.type);
                    position.z += offset;
                    scale.z -= offset;
                }
            }

            if (wire.endTouchesComponent)
            {
                var endComponent = FindComponentAtPosition(circuitData, wire.toASCII);
                if (endComponent != null)
                {
                    float offset = GetWireOffsetForComponentType(endComponent.type);
                    scale.z -= offset;
                }
            }

            // Apply fork/merge offsets
            if (wire.isPartOfFork)
            {
                scale.z -= wireOffsetIfFork;
            }

            if (wire.isPartOfMerge)
            {
                position.z += wireOffsetIfMerge;
            }

            // Apply general wire offset
            if (!wire.startTouchesComponent && !wire.isPartOfMerge)
            {
                position.z += wireOffsetIfWire;
                scale.z -= wireOffsetIfWire;
            }

            if (!wire.endTouchesComponent && !wire.isPartOfFork)
            {
                scale.z -= wireOffsetIfWire;
            }

            wireObject.transform.localPosition = position;
            wireObject.transform.localScale = scale;
        }

        /// <summary>
        /// Finds a component at the specified ASCII position
        /// </summary>
        private Component FindComponentAtPosition(CircuitData circuitData, Vector2Int asciiPosition)
        {
            return circuitData.components.FirstOrDefault(c => c.asciiPosition == asciiPosition);
        }

        /// <summary>
        /// Gets the wire offset value for a specific component type
        /// </summary>
        private float GetWireOffsetForComponentType(string componentType)
        {
            return componentType switch
            {
                "resistor" => wireOffsetIfResistor,
                "lightbulb" => wireOffsetIfLightbulb,
                "battery" => wireOffsetIfBattery,
                "switch" => wireOffsetIfSwitch,
                "fork" => wireOffsetIfFork,
                "merge" => wireOffsetIfMerge,
                _ => 0f
            };
        }

        /// <summary>
        /// Places a component prefab in the scene
        /// </summary>
        private GameObject PlaceComponent(CircuitData circuitData, Component component)
        {
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Attempting to place component of type {component.type} at {component.asciiPosition}", sendToXRDebugLogViewer, sendToDebugLog);
            GameObject prefab = GetPrefabForComponentType(component.type);
            if (prefab == null)
            {
                return null;
            }

            // Instantiate component
            GameObject componentObject = Instantiate(prefab, parentTransform);
            // Calculate position: (asciiPosition.y, 0, asciiPosition.x)
            componentObject.transform.localPosition = new Vector3(component.asciiPosition.y, 0, component.asciiPosition.x);
            componentObject.transform.localRotation = Quaternion.identity;
            XRDebugLogViewer.Log($"CircuitPrefabDrawer: Component instantiated at {componentObject.transform.position}", sendToXRDebugLogViewer, sendToDebugLog);
            
            // Add ComponentData component
            CircuitComponentUI componentData = componentObject.GetComponent<CircuitComponentUI>();
            if (componentData == null)
            {
                XRDebugLogViewer.LogError("CircuitPrefabDrawer: ERROR - CircuitComponentUI component not found on prefab");
                return null;
            }
            componentData.Initialize(component);
            XRDebugLogViewer.Log("CircuitPrefabDrawer: Component placement complete", sendToXRDebugLogViewer, sendToDebugLog);

            return componentObject;
        }

        /// <summary>
        /// Gets the appropriate prefab for a component type
        /// </summary>
        private GameObject GetPrefabForComponentType(string componentType)
        {
            return componentType switch
            {
                "resistor" => resistorPrefab,
                "lightbulb" => lightbulbPrefab,
                "battery" => batteryPrefab,
                "switch" => switchPrefab,
                "fork" => forkPrefab != null ? forkPrefab : null,
                "merge" => mergePrefab != null ? mergePrefab : null,
                _ => null
            };
        }
    }
}