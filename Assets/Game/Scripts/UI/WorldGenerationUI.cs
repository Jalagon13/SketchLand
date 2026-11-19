using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SketchLand
{
    public class WorldGenerationUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _worldGenText;
        [SerializeField] private Image _loadingBarFg;
        [SerializeField] private RectTransform _worldGenUI;

        private void Awake()
        {
            _loadingBarFg.type = Image.Type.Filled;
            _loadingBarFg.fillMethod = Image.FillMethod.Horizontal;
            _loadingBarFg.fillOrigin = (int)Image.OriginHorizontal.Left;
            UpdateWorldGenUI(0f, "Generating world");
        }
    
        private void Start() 
        {
            WorldManager.Instance.WorldGen.OnWorldGenProgressed += UpdateWorldGenUI;
            WorldManager.Instance.OnWorldReady += CloseUI;

            ShowUI();
        }
        
        private void OnDestroy() 
        {
            WorldManager.Instance.WorldGen.OnWorldGenProgressed -= UpdateWorldGenUI;
            WorldManager.Instance.OnWorldReady -= CloseUI;
        }

        private void UpdateWorldGenUI(float progress, string stepName)
        {
            progress = Mathf.Clamp01(progress);
            _worldGenText.text = $"{stepName}. Progress {progress * 100:F0}%";

            _loadingBarFg.fillAmount = progress;
        }
        
        private void ShowUI()
        {
            _worldGenUI.gameObject.SetActive(true);
        }

        private void CloseUI()
        {
            _worldGenUI.gameObject.SetActive(false);
        }
    }
}
