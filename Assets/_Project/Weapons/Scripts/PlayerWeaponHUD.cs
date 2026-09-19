using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheChimeraProtocol.Weapons
{
    /// <summary>
    /// Handles player on-screen HUD:
    /// - Dynamic interaction pickup prompt ("Press [E] to pick up [WeaponName]")
    /// - High-resolution TextMeshProUGUI rendering for razor-sharp text
    /// - Ammunition counter (Current Magazine / Reserve Pool)
    /// - Weapon name and reload status
    /// - Dynamic reticle / crosshair that reacts to movement spread and aim mode.
    /// Auto-generates procedural canvas if no UI prefab is assigned.
    /// </summary>
    public class PlayerWeaponHUD : MonoBehaviour
    {
        [Header("Weapon Controller Reference")]
        [SerializeField] private PlayerWeaponController weaponController;

        [Header("Crosshair Settings")]
        [SerializeField] private Color crosshairColor = new Color(1f, 1f, 1f, 0.75f);
        [SerializeField] private Color crosshairAimColor = new Color(0.95f, 0.25f, 0.25f, 0.95f);
        [SerializeField] private float baseCrosshairGap = 12f;
        [SerializeField] private float aimCrosshairGap = 4f;

        // UI Canvas & Elements
        private Canvas _canvas;
        private CanvasGroup _promptGroup;
        private TextMeshProUGUI _promptText;
        private TextMeshProUGUI _ammoText;
        private TextMeshProUGUI _weaponNameText;
        private TextMeshProUGUI _statusText;

        // Reticle parts
        private RectTransform _crosshairRoot;
        private Image _chTop;
        private Image _chBottom;
        private Image _chLeft;
        private Image _chRight;
        private Image _chDot;
        private CanvasGroup _crosshairGroup;

        private void Awake()
        {
            if (weaponController == null)
            {
                weaponController = GetComponentInParent<PlayerWeaponController>();
                if (weaponController == null)
                {
                    weaponController = FindFirstObjectByType<PlayerWeaponController>();
                }
            }

            BuildHUDCanvas();
        }

        private void Update()
        {
            if (weaponController == null) return;

            UpdatePickupPrompt();
            UpdateAmmoDisplay();
            UpdateCrosshair();
        }

        private void UpdatePickupPrompt()
        {
            var targetPickup = weaponController.CurrentTargetPickup;
            if (targetPickup != null && targetPickup.gameObject.activeInHierarchy && targetPickup.Data != null)
            {
                _promptText.text = $"<color=#FFD54F><b>[ E ]</b></color>   Pick up   <color=#FFFFFF><b>{targetPickup.Data.weaponName}</b></color>";
                _promptGroup.alpha = Mathf.MoveTowards(_promptGroup.alpha, 1.0f, Time.deltaTime * 10f);
            }
            else
            {
                // Smoothly and swiftly fade out as soon as pickup is collected or gone
                _promptGroup.alpha = Mathf.MoveTowards(_promptGroup.alpha, 0.0f, Time.deltaTime * 14f);
            }
        }

        private void UpdateAmmoDisplay()
        {
            var activeWeapon = weaponController.ActiveWeapon;
            if (activeWeapon != null && !weaponController.IsHolstered)
            {
                _weaponNameText.text = activeWeapon.Data.weaponName.ToUpper();

                if (activeWeapon.IsReloading)
                {
                    _ammoText.text = $"{activeWeapon.CurrentAmmoInMag} <size=65%><color=#888888>/</color> {activeWeapon.CurrentReserveAmmo}</size>";
                    _statusText.text = "RELOADING...";
                    _statusText.color = new Color(1f, 0.75f, 0.2f, 1f);
                }
                else
                {
                    _ammoText.text = $"{activeWeapon.CurrentAmmoInMag} <size=65%><color=#888888>/</color> {activeWeapon.CurrentReserveAmmo}</size>";
                    _statusText.text = activeWeapon.Data.fireMode == FireMode.FullAuto ? "FULL AUTO" : "SEMI AUTO";
                    _statusText.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
                }
            }
            else if (weaponController.IsHolstered)
            {
                _weaponNameText.text = "HOLSTERED";
                _ammoText.text = "-- <size=65%><color=#555555>/</color> --</size>";
                _statusText.text = "[X] DRAW WEAPON";
                _statusText.color = new Color(0.7f, 0.7f, 0.7f, 0.75f);
            }
            else
            {
                _weaponNameText.text = "UNARMED";
                _ammoText.text = "-- <size=65%><color=#555555>/</color> --</size>";
                _statusText.text = "";
            }
        }

        private void UpdateCrosshair()
        {
            var activeWeapon = weaponController.ActiveWeapon;
            bool shouldShow = activeWeapon != null && !weaponController.IsHolstered;

            float targetAlpha = shouldShow ? 1.0f : 0.0f;
            _crosshairGroup.alpha = Mathf.MoveTowards(_crosshairGroup.alpha, targetAlpha, Time.deltaTime * 8f);

            if (!shouldShow) return;

            bool isAiming = weaponController.IsAiming;
            float targetGap = isAiming ? aimCrosshairGap : baseCrosshairGap;
            Color curColor = isAiming ? crosshairAimColor : crosshairColor;

            _chTop.color = curColor;
            _chBottom.color = curColor;
            _chLeft.color = curColor;
            _chRight.color = curColor;
            _chDot.color = curColor;

            // Reticle spread positions
            _chTop.rectTransform.anchoredPosition = new Vector2(0, targetGap + 5);
            _chBottom.rectTransform.anchoredPosition = new Vector2(0, -(targetGap + 5));
            _chLeft.rectTransform.anchoredPosition = new Vector2(-(targetGap + 5), 0);
            _chRight.rectTransform.anchoredPosition = new Vector2(targetGap + 5, 0);
        }

        private void OnDestroy()
        {
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
            }
        }

        #region Procedural Canvas Construction
        private void BuildHUDCanvas()
        {
            // Canvas Root (Independent Screen Space Overlay Canvas)
            var canvasObj = new GameObject("WeaponHUD_Canvas");

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // 1. Crisp Interaction Prompt
            var promptObj = new GameObject("InteractionPrompt");
            promptObj.transform.SetParent(canvasObj.transform, false);

            var promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.32f);
            promptRect.anchorMax = new Vector2(0.5f, 0.32f);
            promptRect.sizeDelta = new Vector2(460, 46);

            var promptBg = promptObj.AddComponent<Image>();
            promptBg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);

            _promptGroup = promptObj.AddComponent<CanvasGroup>();
            _promptGroup.alpha = 0f;

            var textObj = new GameObject("PromptText");
            textObj.transform.SetParent(promptObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            _promptText = textObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) _promptText.font = defaultFont;
            _promptText.fontSize = 20;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = new Color(1.0f, 0.95f, 0.8f, 1f);
            _promptText.enableWordWrapping = false;
            _promptText.text = "<color=#FFD54F><b>[ E ]</b></color>   Pick up Weapon";

            // 2. Ammo & Weapon Info Panel (Bottom Right)
            var ammoPanel = new GameObject("AmmoPanel");
            ammoPanel.transform.SetParent(canvasObj.transform, false);

            var ammoPanelRect = ammoPanel.AddComponent<RectTransform>();
            ammoPanelRect.anchorMin = new Vector2(1f, 0f);
            ammoPanelRect.anchorMax = new Vector2(1f, 0f);
            ammoPanelRect.pivot = new Vector2(1f, 0f);
            ammoPanelRect.anchoredPosition = new Vector2(-45f, 40f);
            ammoPanelRect.sizeDelta = new Vector2(250, 100);

            var ammoBg = ammoPanel.AddComponent<Image>();
            ammoBg.color = new Color(0.04f, 0.05f, 0.07f, 0.78f);

            // Weapon Name Text
            var nameObj = new GameObject("WeaponNameText");
            nameObj.transform.SetParent(ammoPanel.transform, false);
            var nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0.65f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            _weaponNameText = nameObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) _weaponNameText.font = defaultFont;
            _weaponNameText.fontSize = 15;
            _weaponNameText.fontStyle = FontStyles.Bold;
            _weaponNameText.characterSpacing = 3f;
            _weaponNameText.alignment = TextAlignmentOptions.Center;
            _weaponNameText.color = new Color(0.92f, 0.86f, 0.68f, 1f);
            _weaponNameText.text = "AR-W ASSAULT RIFLE";

            // Ammo Counter Text
            var countObj = new GameObject("AmmoCountText");
            countObj.transform.SetParent(ammoPanel.transform, false);
            var countRect = countObj.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0.28f);
            countRect.anchorMax = new Vector2(1f, 0.72f);
            _ammoText = countObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) _ammoText.font = defaultFont;
            _ammoText.fontSize = 32;
            _ammoText.fontStyle = FontStyles.Bold;
            _ammoText.alignment = TextAlignmentOptions.Center;
            _ammoText.color = Color.white;
            _ammoText.text = "30 <size=65%><color=#888888>/</color> 120</size>";

            // Status Text (Fire mode or Reloading)
            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(ammoPanel.transform, false);
            var statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0.04f);
            statusRect.anchorMax = new Vector2(1f, 0.32f);
            _statusText = statusObj.AddComponent<TextMeshProUGUI>();
            if (defaultFont != null) _statusText.font = defaultFont;
            _statusText.fontSize = 13;
            _statusText.fontStyle = FontStyles.Bold;
            _statusText.characterSpacing = 2f;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
            _statusText.text = "FULL AUTO";

            // 3. Crosshair (Center)
            var chObj = new GameObject("Crosshair");
            chObj.transform.SetParent(canvasObj.transform, false);

            _crosshairRoot = chObj.AddComponent<RectTransform>();
            _crosshairRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _crosshairRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _crosshairRoot.sizeDelta = new Vector2(40, 40);

            _crosshairGroup = chObj.AddComponent<CanvasGroup>();
            _crosshairGroup.alpha = 0f;

            _chTop = CreateReticleLine(_crosshairRoot, new Vector2(2, 8));
            _chBottom = CreateReticleLine(_crosshairRoot, new Vector2(2, 8));
            _chLeft = CreateReticleLine(_crosshairRoot, new Vector2(8, 2));
            _chRight = CreateReticleLine(_crosshairRoot, new Vector2(8, 2));
            _chDot = CreateReticleLine(_crosshairRoot, new Vector2(3, 3));
        }

        private Image CreateReticleLine(Transform parent, Vector2 size)
        {
            var lineObj = new GameObject("Line");
            lineObj.transform.SetParent(parent, false);

            var rect = lineObj.AddComponent<RectTransform>();
            rect.sizeDelta = size;

            var img = lineObj.AddComponent<Image>();
            img.color = crosshairColor;
            return img;
        }
        #endregion
    }
}
