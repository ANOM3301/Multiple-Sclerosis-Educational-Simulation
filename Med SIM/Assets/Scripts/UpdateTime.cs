using UnityEngine;
using TMPro;
using System;

public class RealTimeClock : MonoBehaviour
{
    private TMP_Text clockText;

    void Start()
    {
        clockText = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (clockText != null)
        {
            clockText.text = DateTime.Now.ToString("hh:mm");
        }
    }
}