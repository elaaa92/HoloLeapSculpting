using System;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    private GameObject mainPanel;
    private GameObject[] panels;
    private int currHover;
    private Material[] materials;
#if UNITY_EDITOR
    private Vector3 canvasPos = 1.9f * Vector3.forward;
    private Vector3 canvasScale = new Vector3(5f,5f,1);
#else
    private Vector3 canvasPos = new Vector3(0,0.17f,1f);
    private Vector3 canvasScale = new Vector3(4.8f,4.8f,0.1f);
#endif

    // Use this for initialization
    void Start ()
    {
        string[] panelnames = { "SplitPanel", "ExtrudePanel", "DeformPanel", "ScalePanel", "RotatePanel", "UndoPanel", "RedoPanel", "SavePanel", "ClosePanel" };
        int npanels = panelnames.Length;

        mainPanel = transform.GetChild(0).gameObject;
        
        currHover = 0;

        materials = new Material[2];
        materials[0] = (Material) Resources.Load("Materials/2D/unselected");
        materials[1] = (Material) Resources.Load("Materials/2D/selected");

        panels = new GameObject[npanels];

        foreach (Transform child in mainPanel.transform)
        {
            int ind = Array.FindIndex(panelnames, n => n == child.name);
            panels[ind] = child.gameObject;
        }

        //mainPanel.transform.position = cameraTransform.TransformPoint(canvasPos);
        mainPanel.transform.position = canvasPos;
        mainPanel.transform.localScale = canvasScale;
        currHover = 3;

        Disable();
    }

    public void Enable()
    {
        currHover = 3;
        mainPanel.SetActive(true);
    }

    public void Disable()
    {
        mainPanel.SetActive(false);
    }

    public Vector3 GetPosition()
    {
        return mainPanel.transform.position;
    }

    public float GetHeight()
    {
        return mainPanel.transform.position.y;
    }

    public float GetRadius()
    {
        return 0.1f;
    }


    public bool GetInteractionPoint(Vector3 handpos, Vector3 dir, out Vector3 interactionPoint)
    {
        return GetInteractionPoint(handpos, dir, Mathf.Infinity, out interactionPoint);
    }

    public bool GetInteractionPoint(Vector3 handpos, Vector3 dir, float maxdist, out Vector3 interactionPoint)
    {
        RaycastHit hit;
        //If raycast hit the sculpture surface, return a position on the surface
        
        if (Physics.Raycast(handpos, dir, out hit, maxdist, LayerMask.GetMask("Button")))
        {
            currHover = Array.FindIndex(panels, g => g.name == hit.collider.gameObject.name);

            for(int i=0; i<panels.Length; i++)
            {
                if(i == currHover)
                    panels[i].GetComponent<Image>().material = materials[1];
                else
                    panels[i].GetComponent<Image>().material = materials[0];
            }
            
            interactionPoint = hit.point - 0.03f * dir;
            return true;
        }
        //Else return a position at fixed distance from the camera
        else
        {
            interactionPoint = handpos + (Vector3.Distance(handpos, GetPosition()) - GetRadius()) * dir;
            for (int i = 0; i < panels.Length; i++)
                panels[i].GetComponent<Image>().material = materials[0];
            currHover = -1;
            return false;
        }
    }

    public int Select()
    {
        return currHover;
    }
}
