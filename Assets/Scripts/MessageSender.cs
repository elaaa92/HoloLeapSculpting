using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageSender : MonoBehaviour {

    private SocketManager socketManager;

    void Start()
    {
        socketManager = gameObject.GetComponent<SocketManager>();


        socketManager = GameObject.FindWithTag("Controller").GetComponent<SocketManager>();
        if (socketManager == null)
            throw new System.Exception("Cannot find controller");
    }


    public void SendPacket(string msg)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        socketManager.SendPacket(msg, timestamp);
    }
}
