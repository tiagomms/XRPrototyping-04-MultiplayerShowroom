using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Events;

/// <summary>
/// Shared data types for circuit processing across different steps
/// </summary>
namespace CircuitProcessor
{
    /// <summary>
    /// Represents a component in the circuit
    /// </summary>
    [Serializable]
    public class Component
    {
        public string id;
        public string type;
        private float value;
        public Vector2Int gridPosition;
        public Vector2Int asciiPosition;
        public Vector2 rectPosition;  // Position in pixels

        [NonSerialized] // UnityEvent can't be serialized by Newtonsoft.Json
        public UnityEvent<Component> OnValueChanged;

        public float Value 
        { 
            get => value;
            private set
            {
                this.value = value;
                OnValueChanged?.Invoke(this);
            }
        }

        public Component(string id, string type, float value, Vector2Int gridPosition, Vector2Int asciiPosition, Vector2 rectPosition)
        {
            this.id = id;
            this.type = type;
            this.value = value;
            this.gridPosition = gridPosition;
            this.asciiPosition = asciiPosition;
            this.rectPosition = rectPosition;
            this.OnValueChanged = new UnityEvent<Component>();
        }

        public void SetValue(float newValue)
        {
            Value = newValue;
        }

        public override bool Equals(object obj)
        {
            if (obj is Component other)
            {
                return id == other.id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return id?.GetHashCode() ?? 0;
        }

        public override string ToString()
        {
            return $"Component {id} of type {type}: {Value}. Positions - Grid {gridPosition}, Circuit {asciiPosition}";
        }
    }

    /// <summary>
    /// Represents a wire in the circuit
    /// </summary>
    [Serializable]
    public class Wire
    {
        public string id;
        public Vector2Int fromGrid;
        public Vector2Int toGrid;
        public Vector2Int fromASCII;
        public Vector2Int toASCII;
        public Vector2 fromRect;  // Position in pixels
        public Vector2 toRect;    // Position in pixels
        public bool isHorizontal;
        public bool startTouchesComponent;
        public bool endTouchesComponent;
        public bool isPartOfFork;
        public bool isPartOfMerge;
    }

    /// <summary>
    /// Represents a violation in the ASCII drawing process
    /// </summary>
    [Serializable]
    public class Violation
    {
        public string type;
        public string component;
        public string message;
        public Vector2Int position;
        public char original;
        public char replaced;
    }

    /// <summary>
    /// Represents the complete circuit data
    /// </summary>
    [Serializable]
    public class CircuitData
    {
        public List<Component> components;
        public List<Wire> wires;
        public string formula;
        public string verbalPlan;
        public List<string> ascii;
        public Vector2Int asciiSize;  // Size of the ASCII canvas
        public Vector2Int imageResolution;  // Resolution of the output image
        public List<Violation> violations;
        public List<object> conditionalBranches;
        public string notes;
        [JsonExtensionData]
        public Dictionary<string, object> additionalData;

        public CircuitData()
        {
            components = new List<Component>();
            wires = new List<Wire>();
            ascii = new List<string>();
            asciiSize = Vector2Int.zero;
            imageResolution = Vector2Int.zero;
            violations = new List<Violation>();
            conditionalBranches = new List<object>();
            additionalData = new Dictionary<string, object>();
        }

        public CircuitData(List<Component> components, List<Wire> wires, string formula, string verbalPlan,
            List<Violation> violations, List<object> conditionalBranches, string notes)
        {
            this.components = components;
            this.wires = wires;
            this.formula = formula;
            this.verbalPlan = verbalPlan;
            ascii = new List<string>();
            asciiSize = Vector2Int.zero;
            imageResolution = Vector2Int.zero;
            this.violations = violations != null ? violations : new List<Violation>();
            this.conditionalBranches = conditionalBranches;
            this.notes = notes;
            additionalData = new Dictionary<string, object>();
        }
    }
}