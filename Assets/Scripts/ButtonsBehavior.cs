using HoloToolkit.Unity;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.Receivers;
using HoloToolkit.Unity.InputModule;

public class ButtonsBehavior : InteractionReceiver
{
    // Use this for initialization
    void Start () {
		
	}

    protected override void InputDown(GameObject obj, InputEventData eventData)
    {
        Debug.Log(obj.name + " : InputDown");
        GestureAction gestureAction = gameObject.GetComponentInParent<GestureAction>();

        switch (obj.name)
        {
            case "MoveButton":
                gestureAction.SetMode(0);
                break;

            case "RotateButton":
                gestureAction.SetMode(1);
                break;

            case "ResizeButton":
                gestureAction.SetMode(2);
                break;

            default:
                break;
        }
    }

    protected override void InputUp(GameObject obj, InputEventData eventData)
    {
    }
}
