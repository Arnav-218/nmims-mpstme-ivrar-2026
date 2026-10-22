using UnityEngine;

public enum PhishingSimulationAction
{
    ClickLink,
    ReportPhishing,
    Ignore,
    Continue,
    Restart
}

public sealed class PhishingActionButton : MonoBehaviour
{
    [SerializeField] private PhishingSimulationAction action;
    [SerializeField] private Color baseColor = Color.white;

    private Renderer buttonRenderer;
    private Collider buttonCollider;
    private bool isInteractable;

    public PhishingSimulationAction Action
    {
        get { return action; }
    }

    public bool IsInteractable
    {
        get { return isInteractable; }
    }

    private void Awake()
    {
        buttonRenderer = GetComponent<Renderer>();
        buttonCollider = GetComponent<Collider>();
        SetInteractable(action == PhishingSimulationAction.Restart);
    }

    public void Initialize(PhishingSimulationAction buttonAction, Color color)
    {
        action = buttonAction;
        baseColor = color;
        buttonRenderer = GetComponent<Renderer>();
        buttonCollider = GetComponent<Collider>();
        SetInteractable(action == PhishingSimulationAction.Restart);
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (buttonCollider != null)
            buttonCollider.enabled = interactable;
        SetColor(interactable ? baseColor : Color.Lerp(baseColor, Color.black, 0.55f));
    }

    public void SetHovered(bool isHovered)
    {
        if (buttonRenderer != null && isInteractable)
            SetColor(isHovered ? Color.Lerp(baseColor, Color.white, 0.45f) : baseColor);
    }

    private void SetColor(Color color)
    {
        if (buttonRenderer == null)
            return;

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        buttonRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        buttonRenderer.SetPropertyBlock(properties);
    }
}
