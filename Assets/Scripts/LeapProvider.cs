using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeapProvider : MonoBehaviour {
    private SocketManager socketManager;
    private bool isLeapActive;
    private LeapFrame currentFrame;

    void Awake()
    {
        socketManager = gameObject.AddComponent<SocketManager>();
        gameObject.AddComponent<MessageSender>();
    }

    void Start()
    {
        isLeapActive = true;
        currentFrame = null;
    }

    void Update()
    {
        currentFrame = null;
        string lastMessage = socketManager.getData();

        if (lastMessage != null)
        {
            if (!isLeapActive)
            {
                Debug.Log("Connection with leap restablished");
                isLeapActive = true;
            }

            LeapFrame frame = JsonConvert.DeserializeObject<LeapFrame>(lastMessage);
            if (!frame.IsEmpty)
            {
                currentFrame = frame;
            }
        }
        else
        {
            if (isLeapActive)
            {
                isLeapActive = false;
                Debug.LogWarning("Leap is not responding");
            }
        }
    }

    public LeapFrame GetCurrentFrame()
    {
        LeapFrame frame = currentFrame;
        currentFrame = null;
        return frame;
    }
}
