using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class testScript : MonoBehaviour
{
    [SerializeField] private GameObject mainCameraObject;
    [SerializeField] private GameObject testCameraObject;
    [SerializeField] private GameObject pencilObject;
    [SerializeField] private Image crosshair;

    [SerializeField] private Collider paperCollider;
    [SerializeField] private GameObject linePrefab;
    [SerializeField] private Transform pencilTip;

    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip numbClip;
    [SerializeField] private AudioClip frozenClip;

    private Vector3 pencilStartLocalPosition;
    private Quaternion pencilStartLocalRotation;
    private Vector3 pencilRotationOffset = new Vector3(60f, 90f, 60f);
    private Vector3 pencilPositionOffset = new Vector3(0.15f, -0.06f, 0f);

    public MoveWithMouse mainCamera;
    public MSController MSStatus;

    private Camera testCamera;
    public bool isViewingTest = false;

    private float pitch = 0f;
    private float yaw = 0f;

    private Vector2 virtualCursorPosition;
    private float cursorSensitivity = 2f;

    private LineRenderer currentLine;
    private List<Vector3> linePoints = new List<Vector3>();

    private float tremorAmount = 0.05f;
    private float tremorSpeed = 25f;

    private Vector3 tremor
    {
        get
        {
            return new Vector3(
                Mathf.PerlinNoise(Time.time * tremorSpeed, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, Time.time * tremorSpeed) - 0.5f,
                0f
            ) * tremorAmount;
        }
    }

    private float numbChancePerSecond = 0.05f;
    private bool handNumb = false;
    private float numbEndTime = 0f;

    [SerializeField] private Vector2 numbDuration = new Vector2(1f, 3f);

    private float fatigueChancePerSecond = 0.03f;
    private bool mentallyFrozen = false;
    private float freezeEndTime;

    [SerializeField] private Vector2 fatigueDuration = new Vector2(2f, 5f);

    void Start()
    {
        pencilStartLocalPosition = pencilObject.transform.localPosition;
        pencilStartLocalRotation = pencilObject.transform.localRotation;

        if (testCameraObject != null)
        {
            testCamera = testCameraObject.GetComponent<Camera>();
        }

        hideTest();
    }

    void Update()
    {
        if (mainCamera != null)
        {
            pitch = mainCamera.currentPitch;
            yaw = mainCamera.currentYaw;
        }

        // Handle camera switching transitions
        if (mainCamera != null)
        {
            pitch = mainCamera.currentPitch;
            yaw = mainCamera.currentYaw;
        }

        if (!isViewingTest)
        {
            if (pitch > 40 && yaw > -30 && yaw < 45 && Mouse.current.leftButton.wasPressedThisFrame)
            {
                showTest();
                return;
            }
        }


        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            hideTest();
        }

        if (isViewingTest)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            virtualCursorPosition += delta * cursorSensitivity;

            virtualCursorPosition.x = Mathf.Clamp(virtualCursorPosition.x, 0, Screen.width);
            virtualCursorPosition.y = Mathf.Clamp(virtualCursorPosition.y, 0, Screen.height);

            if (MSStatus.MSActive && !mentallyFrozen)
            {
                if (Random.value < fatigueChancePerSecond * Time.deltaTime)
                {
                    mentallyFrozen = true;
                    freezeEndTime = Time.time + Random.Range(fatigueDuration.x, fatigueDuration.y);
                    Debug.Log("You feel mentally frozen for a moment.");
                    audioSource.clip = frozenClip;
                    audioSource.loop = false;
                    audioSource.Play();
                }
            }

            if (mentallyFrozen && Time.time >= freezeEndTime)
            {
                mentallyFrozen = false;
            }

            if (MSStatus.MSActive && !handNumb)
            {
                if (Random.value < numbChancePerSecond * Time.deltaTime)
                {
                    handNumb = true;
                    numbEndTime = Time.time + Random.Range(numbDuration.x, numbDuration.y);
                    Debug.Log("You feel your hand go numb for a moment.");
                    audioSource.clip = numbClip;
                    audioSource.loop = false;
                    audioSource.Play();
                }
            }

            if (handNumb && Time.time >= numbEndTime)
            {
                handNumb = false;
            }

            UpdatePencilPosition();
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartDrawing();
            }

            if (Mouse.current.leftButton.isPressed && currentLine != null && !handNumb && !mentallyFrozen)
            {
                ContinueDrawing();
            }

            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                StopDrawing();
            }
        }
    }

    private void StartDrawing()
    {
        Vector3 mousePos = GetMouseWorldPosition();

        if (mousePos == Vector3.positiveInfinity)
        {
            Debug.Log("Missed paper");
            return;
        }

        if (MSStatus.MSActive)
        {
            mousePos += tremor;
        }

        linePoints.Clear();

        GameObject newLineObj = Instantiate(linePrefab);
        currentLine = newLineObj.GetComponentInChildren<LineRenderer>();

        if (currentLine == null)
        {
            Debug.LogError("Line prefab has no LineRenderer!");
            return;
        }

        AddPoint(mousePos);
        AddPoint(mousePos);
    }

    private void ContinueDrawing()
    {
        Vector3 mousePos = GetMouseWorldPosition();

        if (mousePos == Vector3.positiveInfinity)
            return;

        if (MSStatus.MSActive)
        {
            mousePos += tremor;
        }

        AddPoint(mousePos);
    }

    private void StopDrawing()
    {
        currentLine = null;
    }

    private void AddPoint(Vector3 point)
    {
        linePoints.Add(point);

        currentLine.positionCount = linePoints.Count;
        currentLine.SetPosition(linePoints.Count - 1, point);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = testCamera.ScreenPointToRay(virtualCursorPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log(hit.collider.name);
            if (hit.collider == paperCollider)
            {
                return hit.point;
            }
        }

        return Vector3.positiveInfinity;
    }

    void showTest()
    {
        testCameraObject.SetActive(true);
        mainCameraObject.SetActive(false);
        crosshair.gameObject.SetActive(false);
        isViewingTest = true;

        virtualCursorPosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
    }

    void hideTest()
    {
        StopDrawing();

        pencilObject.transform.localPosition = pencilStartLocalPosition;
        pencilObject.transform.localRotation = pencilStartLocalRotation;

        testCameraObject.SetActive(false);
        mainCameraObject.SetActive(true);
        crosshair.gameObject.SetActive(true);
        isViewingTest = false;
    }

    private void UpdatePencilPosition()
    {
        Ray ray = testCamera.ScreenPointToRay(virtualCursorPosition);

        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == paperCollider)
        {
            Quaternion rotation =
                Quaternion.LookRotation(-hit.normal, Vector3.up) *
                Quaternion.Euler(60f, 90f, 60f);

            pencilObject.transform.rotation = rotation;

            Vector3 tipOffset = pencilObject.transform.position - pencilTip.position;
            Vector3 tremor = Vector3.zero;

            if (MSStatus.MSActive)
            {
                tremor = new Vector3(
                    Mathf.PerlinNoise(Time.time * tremorSpeed, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, Time.time * tremorSpeed) - 0.5f,
                    0f
                ) * tremorAmount;
            }

            pencilObject.transform.position =
                hit.point + rotation * pencilPositionOffset + tremor;
        }
    }
}

public static class CameraExtensions
{
    public static Vector3 ScreenToWorldSpaceSource(this Camera camera, Vector3 screenPos)
    {
        return camera.ScreenToWorldPoint(screenPos);
    }
}