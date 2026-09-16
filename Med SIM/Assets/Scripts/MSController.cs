using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MSController : MonoBehaviour
{
    [SerializeField] private Image MSStatus;
    [SerializeField] private Sprite MSOff;
    [SerializeField] private Sprite MSOn;

    [SerializeField] private Image crosshair;
    [SerializeField] private Sprite defaultCrosshair;
    [SerializeField] private Sprite MSCrosshair;

    public testScript test;

    public bool MSActive = false;

    private RectTransform rectTrans;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MSActive = false;
        rectTrans = crosshair.GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            MSActive = !MSActive;
        }

        if (MSActive)
        {
            MSStatus.sprite = MSOn;

            if (!test.isViewingTest)
            {
                crosshair.sprite = MSCrosshair;
                rectTrans.sizeDelta = new Vector2(1000f, 1000f);
            }
        }
        else
        {
            MSStatus.sprite = MSOff;

            if (!test.isViewingTest)
            {
                crosshair.sprite = defaultCrosshair;
                rectTrans.sizeDelta = new Vector2(250f, 250f);
            }
        }
    }
}