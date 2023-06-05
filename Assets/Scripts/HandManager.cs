using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HandManager : PointerManager
{
    public GameObject holoHand;
    public LeapHandModel rightHand;
    public LeapHandModel leftHand;
    private GameObject holoHandInstance;

    protected override void InitPointer()
    {
#if UNITY_EDITOR
#endif
    }

    protected override void UpdatePointer(LeapHand[] hands, bool visible)
    {
        holoHandInstance.SetActive(false);
        if (visible)
        {
            foreach (LeapHand hand in hands)
            {
                if (hand.isLeft)
                    leftHand.UpdateHand(hand);
                else
                    rightHand.UpdateHand(hand);


                /*if (ismenuopen)
                    hit = menuManager.GetInteractionPoint(hand.PalmPosition, hand.Direction, out interactionPoint);
                else
                    hit = sculptureManager.GetInteractionPoint(hand.PalmPosition, hand.Direction, out interactionPoint);

                if (hit)
                    focusType = Focus.onsculpture;
                else
                    focusType = Focus.offsculpture;

                if (hand.PinchStrength > 0.9)
                    inputType = Input.click;
                else if ((inputType == Input.click || inputType == Input.hold) && hand.PinchStrength > 0.7)
                    inputType = Input.hold;
                else
                    inputType = Input.none;*/
            }
        }
    }

    protected override void UpdateHoloPointer(Vector3 currpos)
    {
        if(holoHandInstance == null)
            holoHandInstance = Instantiate(holoHand);
        else
            holoHand.SetActive(true);
        holoHand.transform.position = currpos;
    }
}
