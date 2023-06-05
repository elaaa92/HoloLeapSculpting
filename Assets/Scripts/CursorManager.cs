using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class CursorManager: PointerManager
{
    public GameObject[] cursorStates;
    public Vector3 originalScale;
    public float originalRange;
    bool onmenu;
    protected override void InitPointer()
    {
#if UNITY_EDITOR
        float dim = 10f;
#else
        float dim = 20f;
#endif
        originalScale = cursorStates[0].transform.localScale;
        originalRange = cursorStates[0].transform.GetChild(0).GetComponent<Light>().range;
        ResizeCursor(dim);
        target = Target.sculpture;
    }

    protected void ResizeCursor(float scale)
    {
        foreach (GameObject cursorstate in cursorStates)
        {
            cursorstate.transform.localScale = originalScale * scale;
            cursorstate.transform.GetChild(0).GetComponent<Light>().range = originalRange * scale;
        }
    }

    protected override void UpdatePointer(LeapHand[] hands, bool visible)
    {
        if (hands == null)
        {
            focusType = Focus.unfocused;
        }
        else
        {
            Vector3 cameraaxisalignedforward = calibrationManager.GetAxisAlignedCameraForward(), cameraaxisalignedupward = calibrationManager.GetAxisAlignedCameraUpward();
            Vector3 cameraPos = calibrationManager.GetCameraPosition();
            int nhands = hands.Length;

            LeapHand hand = hands[0];
           
            Vector3 interactionPoint, pos = hand.PalmPosition;
            bool hit = false;
            bool resize = false;
            float res = 0;

            int input = gestureManager.GetGesture(hand, cameraaxisalignedforward, cameraaxisalignedupward, !onmenu && (mode == Mode.rotate_2 || mode == Mode.deform_2 || mode == Mode.extrude_2));

            //if (inputType == Input.pointing && (pointingcount > 0 && pointingcount <= 2))
            if (target != Target.menu && input == -1)
            {
                target = Target.menu;
                inputType = Input.none;
                focusType = Focus.unfocused;
                resize = true;
                res = 0.3f;
                onmenu = true;
            }
            else
            {
                switch (input)
                {
                    //Click event
                    case 0:
                        if (inputType == Input.none)
                            inputType = Input.click;
                        else
                            inputType = Input.none;
                        break;
                    //Hold still event (converted to click in case of click event lost packet)
                    case 1:
                        if (inputType == Input.click || inputType == Input.hold)
                            inputType = Input.hold;
                        else
                            inputType = Input.click;
                        break;
                    //Hold moving event (cannot be converted, unreliable)
                    case 2:
                        if (onmenu)
                            inputType = Input.none;
                        else if (inputType == Input.click || inputType == Input.hold)
                            inputType = Input.hold;
                        break;
                    //No action
                    default:
                        inputType = Input.none;
                        break;
                }

                if (target == Target.menu)
                {
                    hit = menuManager.GetInteractionPoint(hand.PalmPosition, cameraaxisalignedforward, out interactionPoint);
                }
                else
                {
                    if (onmenu)
                    {
                        resize = true;
                        res = 20;
                        onmenu = false;
                    }
                    hit = sculptureManager.GetInteractionPoint(hand.PalmPosition, cameraaxisalignedforward, out interactionPoint);
                }
                
                if (hit)
                    focusType = Focus.focused;
                else
                    focusType = Focus.unfocused;
                //Debug.Log(onmenu + " " + input + " " + inputType);
                if(visible)
                    DrawCursor(interactionPoint, resize, res);
            }
        }
    }

    protected override void UpdateHoloPointer(Vector3 currpos)
    {
        DrawCursor(calibrationManager.currentPos, false, 0);
    }

    void DrawCursor(Vector3 pos, bool resize, float res)
    {
        foreach (GameObject cursorState in cursorStates)
            cursorState.SetActive(false);

        if (mode == Mode.calibrate)
            cursorStates[0].SetActive(true);
        else if (inputType == Input.click)
            cursorStates[1].SetActive(true);
        else if (inputType == Input.hold)
            cursorStates[2].SetActive(true);
        else if (inputType == Input.none)
            cursorStates[3].SetActive(true);
        transform.position = pos;

        if (resize)
            ResizeCursor(res);
    }
}
