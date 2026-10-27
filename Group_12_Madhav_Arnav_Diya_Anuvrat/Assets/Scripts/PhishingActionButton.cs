using System.Collections;
using UnityEngine;
using IVRAR.CybersecurityVR;

public enum PhishingSimulationAction
{
    ClickLink,
    ReportPhishing,
    Ignore,
    Continue,
    Restart,
    Shuffle
}

public sealed class PhishingActionButton : MonoBehaviour
{
    [SerializeField] private PhishingSimulationAction action;
    [SerializeField] private Color baseColor = Color.white;

    private Renderer buttonRenderer;
    private Collider buttonCollider;
    private Material buttonMaterial;
    private Vector3 defaultLocalPos;
    private bool isInteractable = true;
    private bool isHovered = false;
    private Coroutine pressAnimationCoroutine;

    public PhishingSimulationAction Action => action;
    public bool IsInteractable => isInteractable;

    private void Awake()
    {
        InitializeComponents();
        SetInteractable(action == PhishingSimulationAction.Restart || action == PhishingSimulationAction.Shuffle);
    }

    private void InitializeComponents()
    {
        if (buttonRenderer == null)
            buttonRenderer = GetComponent<Renderer>();
        if (buttonCollider == null)
            buttonCollider = GetComponent<Collider>();

        if (buttonRenderer != null && buttonMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = buttonRenderer.sharedMaterial != null ? buttonRenderer.sharedMaterial.shader : Shader.Find("Unlit/Color");

            buttonMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            buttonRenderer.material = buttonMaterial;
        }

        if (defaultLocalPos == Vector3.zero)
            defaultLocalPos = transform.localPosition;
    }

    public void Initialize(PhishingSimulationAction buttonAction, Color color)
    {
        action = buttonAction;
        baseColor = color;
        InitializeComponents();
        SetInteractable(action == PhishingSimulationAction.Restart || action == PhishingSimulationAction.Shuffle);
    }

    public void SetInteractable(bool interactable)
    {
        InitializeComponents();
        isInteractable = interactable;
        if (buttonCollider != null)
            buttonCollider.enabled = interactable;

        Color targetColor = interactable ? baseColor : Color.Lerp(baseColor, new Color(0.18f, 0.20f, 0.22f), 0.65f);
        ApplyColor(targetColor);

        // Reset press displacement if disabled
        if (!interactable && defaultLocalPos != Vector3.zero)
            transform.localPosition = defaultLocalPos;
    }

    public void SetHovered(bool hovered)
    {
        InitializeComponents();
        if (!isInteractable)
            return;

        isHovered = hovered;
        Color targetColor = hovered ? Color.Lerp(baseColor, Color.white, 0.40f) : baseColor;
        ApplyColor(targetColor);

        // Subtle hover displacement when not actively playing press animation
        if (pressAnimationCoroutine == null && defaultLocalPos != Vector3.zero)
        {
            transform.localPosition = hovered
                ? defaultLocalPos + new Vector3(0f, -0.003f, 0.004f)
                : defaultLocalPos;
        }
    }

    public void TriggerPressFeedback()
    {
        if (PhishingAudioManager.Instance != null)
            PhishingAudioManager.Instance.PlayButtonClick(transform.position);

        if (pressAnimationCoroutine != null)
            StopCoroutine(pressAnimationCoroutine);
        pressAnimationCoroutine = StartCoroutine(AnimatePressPlunge());
    }

    private IEnumerator AnimatePressPlunge()
    {
        Vector3 pressedPos = defaultLocalPos + new Vector3(0f, -0.012f, 0.008f);
        float elapsed = 0f;
        float plungeDuration = 0.06f;
        float returnDuration = 0.08f;

        // Plunge down
        while (elapsed < plungeDuration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(defaultLocalPos, pressedPos, elapsed / plungeDuration);
            yield return null;
        }

        // Return up with spring snap-back
        elapsed = 0f;
        Vector3 targetReturn = isHovered ? defaultLocalPos + new Vector3(0f, -0.003f, 0.004f) : defaultLocalPos;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(pressedPos, targetReturn, elapsed / returnDuration);
            yield return null;
        }

        transform.localPosition = targetReturn;
        pressAnimationCoroutine = null;
    }

    private void ApplyColor(Color color)
    {
        if (buttonMaterial != null)
        {
            buttonMaterial.color = color;
            if (buttonMaterial.HasProperty("_BaseColor"))
                buttonMaterial.SetColor("_BaseColor", color);
            if (buttonMaterial.HasProperty("_EmissionColor"))
            {
                buttonMaterial.EnableKeyword("_EMISSION");
                buttonMaterial.SetColor("_EmissionColor", isHovered ? color * 0.45f : color * 0.08f);
            }
        }

        if (buttonRenderer != null)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            buttonRenderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            buttonRenderer.SetPropertyBlock(properties);
        }
    }
}
