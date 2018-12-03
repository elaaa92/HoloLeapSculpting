using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class GestureManager : MonoBehaviour {
    public bool calibrated = false;
    public CalibrationManager calibrationManager;

    // Use this for initialization
    void Start () {
        //Calibrate();
    }

    // Update is called once per frame
    void Update () {
        //lastFrame = JsonConvert.DeserializeObject<LeapFrame>(gameObject.GetComponent<SocketManager>().GetLastInfo());
    }

    void Calibrate()
    {
        //calibrationManager.enabled = true;
        Debug.Log("Calibration start");
        while(!calibrated);
        //calibrationManager.enabled = false;
        Debug.Log("Calibration completed");
    }
}
