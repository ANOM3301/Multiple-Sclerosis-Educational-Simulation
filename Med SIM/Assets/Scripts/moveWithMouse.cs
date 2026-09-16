using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MoveWithMouse : MonoBehaviour
{
    public MSController MSStatus;

    private float nystagmusAmplitude = 0.5f;

    [SerializeField] private Image lhermitteSign;
    [SerializeField] private Sprite flash;

    private bool fadingFlash = false;
    private float flashFadeStartTime;
    private float flashFadeEndTime;
    private float flashFadeDuration = 1f;
    private bool hasBeenFlashed = false;
    private float flashEndTime = 0f;

    private Vector2 mouseDelta;
    private float mouseSensitivity = 0.1f;
    public float currentYaw = 0f;
    public float currentPitch = 0f;
    public bool isLocked = true;

    void Start()
    {
        HideFlash();
        LockCursor();

    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isLocked) UnlockCursor();
            else LockCursor();
        }

        if (isLocked && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            currentYaw += mouseDelta.x * mouseSensitivity;
            currentPitch -= mouseDelta.y * mouseSensitivity;

            currentYaw = Mathf.Clamp(currentYaw, -60f, 60f);
            currentPitch = Mathf.Clamp(currentPitch, -65f, 65f);

            if (MSStatus.MSActive)
            {
                nystagmusAmplitude = Mathf.Sin(Time.time * 25f) * 1.5f;
                currentYaw += nystagmusAmplitude;
            }

            transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

            if (MSStatus.MSActive)
            {
                if (hasBeenFlashed)
                {
                    if (Time.time >= flashEndTime && !fadingFlash)
                    {
                        fadingFlash = true;
                        flashFadeStartTime = Time.time;
                    }
                }
                else
                {
                    if (currentPitch > 25f)
                    {
                        ShowFlash();
                    }
                }
            }
        }

        if (MSStatus.MSActive && fadingFlash)
        {
            float t = (Time.time - flashFadeStartTime) / flashFadeDuration;

            Color c = lhermitteSign.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            lhermitteSign.color = c;

            if (t >= 1f)
            {
                fadingFlash = false;
                HideFlash();
            }
        }

        if (!isLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isLocked = true;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isLocked = false;
    }

    private void HideFlash()
    {
        lhermitteSign.gameObject.SetActive(false);
        lhermitteSign.sprite = null;

        Color c = lhermitteSign.color;
        c.a = 1f;
        lhermitteSign.color = c;

        if (currentPitch <= 25f)
        {
            hasBeenFlashed = false;
        }
    }

    private void ShowFlash()
    {
        lhermitteSign.sprite = flash;
        lhermitteSign.gameObject.SetActive(true);

        Color c = lhermitteSign.color;
        c.a = 1f;
        lhermitteSign.color = c;

        flashEndTime = Time.time + 0.5f;
        hasBeenFlashed = true;
        fadingFlash = false;
    }
}