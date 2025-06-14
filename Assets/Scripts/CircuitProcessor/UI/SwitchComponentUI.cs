using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitProcessor
{
    public class SwitchComponentUI : CircuitComponentUI
    {
        [Header("Toggle References")]
        [SerializeField] private Toggle toggle;
        [SerializeField] private GameObject toggleParent;
        

        public override void Initialize(Component component)
        {
            hasEditableUI = true;
            editableType = EditableType.Toggle;

            base.Initialize(component);
            toggleParent.SetActive(true);

            bool isOn = Mathf.Approximately(component.Value, 1f);
            toggle.isOn = isOn;
            toggle.onValueChanged.AddListener(OnToggleChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
        }

        protected override string WriteDisplayUIText()
        {
            return $"{id}\n{(Mathf.Approximately(component.Value, 1f) ? "ON" : "OFF")}";
        }

        private void OnToggleChanged(bool newState)
        {
            component.SetValue(newState ? 1f : 0f);
            UpdateDisplayUI();
        }
    }
}