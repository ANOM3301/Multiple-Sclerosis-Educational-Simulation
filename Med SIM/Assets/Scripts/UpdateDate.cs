using UnityEngine;
using TMPro;
using System;

public class RealTimeDate : MonoBehaviour
{
    private TMP_Text dateText;

    void Start()
    {
        dateText = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (dateText != null)
        {
            dateText.text = DateTime.Now.ToString("ddd, MMM dd, yyyy");
        }
    }
}