using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Utils;

namespace CircuitProcessor
{
    /// <summary>
    /// Component data to be attached to instantiated component prefabs
    /// </summary>
    // FIXME: separation of concerns - ideally I would make a super class for the CircuitComponentUI prefab
    // FIXME:   that based on the component type, enable, disable stuff. but don't have time. 
    // FIXME:   then have in the parent object of all a reference to this CircuitComponentUI
    public abstract class CircuitComponentUI : MonoBehaviour
    {
        public enum EditableType
        {
            None = 0,
            Slider = 1,
            Toggle = 2
        }

        protected Component component;

        [Header("Display UI")]
        // Reference to your display UI root
        [SerializeField] protected GameObject displayUI;
        // Optional: display value text
        [SerializeField] protected TextMeshProUGUI displayText;
        [SerializeField] protected string unitOfReference;

        [Header("Editable UI")]
        [SerializeField] protected bool hasEditableUI = false;
        [SerializeField] protected GameObject editableUI;
        [SerializeField] protected EditableType editableType = EditableType.None;
        [SerializeField] protected TextMeshProUGUI editableDisplayUIText;
        [SerializeField] protected GameObject editableOptions;

        [Header("Editable UI Offsets")]
        [SerializeField] protected float initialOffset = 9.5f;
        [SerializeField] protected float gridFactor = 1.5f;

        public UnityEvent<Component> OnComponentValueChanged;

        protected CircuitFormulaEvaluator formulaEvaluator;

        public string id => component.id;
        public string type => component.type;
        public float value => component.Value;
        public Vector2Int gridPosition => component.gridPosition;
        public Vector2Int asciiPosition => component.asciiPosition;
        public Vector2 rectPosition => component.rectPosition;

        public virtual void Initialize(Component component)
        {
            this.component = component;
            // NOTE: editable UI should be placed at the bottom of the table and ideally never let it
            // NOTE: since editableUI is child of the object, we need to counter the offset of the parent x position
            // NOTE: by observation - a grid factor looks good 
            Vector3 newLocalPosition = editableUI.transform.localPosition;
            newLocalPosition.x = (initialOffset + component.gridPosition.y * gridFactor - transform.localPosition.x);
            editableUI.transform.localPosition = newLocalPosition;
            editableUI.SetActive(hasEditableUI);

            for (int i = 0; i < editableOptions.transform.childCount; i++)
            {
                var child = editableOptions.transform.GetChild(i);
                child.gameObject.SetActive(false);
            }
            UpdateDisplayUI();
        }

        protected virtual string WriteDisplayUIText()
        {
            return $"{id}\n{NumberFormatter.FormatWithUnit(value, unitOfReference, 2)}";
        }

        protected virtual void UpdateDisplayUI()
        {
            string uiText = WriteDisplayUIText();
            if (displayText != null)
            {
                displayText.text = uiText;
            }
            if (hasEditableUI && editableDisplayUIText != null)
            {
                editableDisplayUIText.text = uiText;
            }
        }

        public virtual void AttachFormulaEvaluator(CircuitFormulaEvaluator formulaEvaluator)
        {
            this.formulaEvaluator = formulaEvaluator;
        }

        public virtual void DetachFormulaEvaluator()
        {
            if (formulaEvaluator != null)
            {
                this.formulaEvaluator = null;
            }
        }

        protected virtual void OnDestroy()
        {
            DetachFormulaEvaluator();
        }
    }
}