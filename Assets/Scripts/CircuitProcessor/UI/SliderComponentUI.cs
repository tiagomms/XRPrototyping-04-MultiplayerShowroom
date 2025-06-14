using System;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace CircuitProcessor
{
    public class SliderComponentUI : CircuitComponentUI
    {
        [Header("Slider References")]
        [SerializeField] private GameObject sliderParent;
        [SerializeField] private Slider slider;
        
        [SerializeField] protected float minValue = 0f;
        [SerializeField] protected float maxValue = 10f;
        [SerializeField] protected bool wholeNumbers = true;

        [SerializeField] private TextMeshProUGUI minValueUIText;
        [SerializeField] private TextMeshProUGUI maxValueUIText;


        public override void Initialize(Component component)
        {
            hasEditableUI = true;
            editableType = EditableType.Slider;

            base.Initialize(component);

            sliderParent.gameObject.SetActive(true);

            minValueUIText.text = NumberFormatter.FormatRoundedAbbreviation(minValue, 0);
            maxValueUIText.text = NumberFormatter.FormatRoundedAbbreviation(maxValue, 0);

            slider.wholeNumbers = wholeNumbers;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.value = component.Value;
            slider.onValueChanged.AddListener(OnSliderChanged);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        private void OnSliderChanged(float newValue)
        {
            component.SetValue(newValue);
            UpdateDisplayUI();
        }
    }
}
