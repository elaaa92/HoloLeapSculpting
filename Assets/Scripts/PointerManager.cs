using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public abstract class PointerManager : MonoBehaviour
{
    protected CalibrationManager calibrationManager;
    private LeapProvider leapProvider;
    protected SculptureManager sculptureManager;
    protected MenuManager menuManager;
    protected GestureManager gestureManager;

    protected enum Mode { calibrate, split_1, split_2, scale_1, scale_2, rotate_1, rotate_2, extrude_1, extrude_2, deform_1, deform_2};
    protected Mode mode;
    protected enum Input { click, hold, none };
    protected Input inputType;
    protected enum Focus { focused, unfocused };
    protected Focus focusType;

    protected enum Target { sculpture, menu };
    protected Target target;

    private bool ismenuopen;
    private bool firstselection;

    private void Awake()
    {
        calibrationManager = gameObject.AddComponent<CalibrationManager>();
        leapProvider = gameObject.GetComponent<LeapProvider>();
        calibrationManager.ManualCalibration = true;
    }

    void Start()
    {
        sculptureManager = GameObject.Find("Editor").GetComponent<SculptureManager>();
        menuManager = GameObject.Find("Menu").GetComponent<MenuManager>();
        gestureManager = new GestureManager();
        mode = Mode.calibrate;
        inputType = Input.none;
        focusType = Focus.unfocused;
        target = Target.sculpture;
        firstselection = true;
        ismenuopen = false;
        InitPointer();
    }

    void Update()
    {
        if (mode == Mode.calibrate)
        {
            UpdateHoloPointer(calibrationManager.currentPos);
            if (calibrationManager.IsCalibrated)
                mode = Mode.split_1;
        }
        else
        {
            LeapFrame frame = leapProvider.GetCurrentFrame();
            LeapHand[] hands;
            bool showpointer = true;
            
            if (frame != null && !frame.IsEmpty)
            {
                if (!ismenuopen && target == Target.menu)
                {
                    OpenMenu();
                }
                else if (ismenuopen && inputType == Input.click)
                {
                    MenuAction(menuManager.Select());
                }
                //Split 1 : split preview
                else if (mode == Mode.split_1)
                {
                    if (inputType == Input.click && focusType == Focus.focused)
                    {
                        sculptureManager.ResetSelection();
                        sculptureManager.SelectFace();
                        sculptureManager.StartSplitPreview();

                        mode = Mode.split_2;
                    }
                }
                //Split 2 : split apply
                else if (mode == Mode.split_2)
                {
                    if (inputType == Input.click || focusType == Focus.unfocused)
                    {
                        if (inputType == Input.click)
                            sculptureManager.Split();

                        sculptureManager.StopSplitPreview();
                        sculptureManager.ResetSelection();

                        mode = Mode.split_1;
                    }
                }
                //Extrude 1 : face selection
                else if (mode == Mode.extrude_1 || mode == Mode.deform_1)
                {
                    //Reset selection become true when switching from split or an extrude has been completed
                    if (firstselection && inputType == Input.click && focusType == Focus.focused)
                    {
                        sculptureManager.ResetSelection();
                        if (mode == Mode.extrude_1)
                            sculptureManager.StartSelectionPreview(0);
                        else
                            sculptureManager.StartSelectionPreview(1);
                        //If there are no faces
                        firstselection = !sculptureManager.SelectFace();
                    }
                    else if (!firstselection && inputType == Input.click && focusType == Focus.focused)
                    {
                        if (inputType == Input.click && mode == Mode.extrude_1 && sculptureManager.SelectHandle())
                        {
                            sculptureManager.StopSelectionPreview();
                            sculptureManager.StartExtrusionPreview();
                            mode = Mode.extrude_2;
                        }
                        else if (inputType == Input.click && mode == Mode.deform_1 && sculptureManager.SelectHandle())
                        {
                            sculptureManager.StopSelectionPreview();
                            sculptureManager.StartMoveEdgePreview();
                            mode = Mode.deform_2;
                        }
                        else
                            firstselection = !sculptureManager.SelectFace();
                    }
                    else if (!firstselection && inputType == Input.click && focusType == Focus.unfocused)
                        sculptureManager.ResetSelection();
                }
                //Extrude 2 : extrusion preview
                else if (mode == Mode.extrude_2)
                {
                    firstselection = true;
                    if (inputType == Input.none)
                    {
                        sculptureManager.Extrude();
                        sculptureManager.StopExtrusionPreview();
                        mode = Mode.extrude_1;
                    }
                }
                //Extrude 2 : vertex deformation preview
                else if (mode == Mode.deform_2)
                {
                    firstselection = true;
                    if (inputType == Input.none)
                    {
                        sculptureManager.MoveEdge();
                        sculptureManager.StopMoveEdgePreview();
                        sculptureManager.ResetSelection();
                        mode = Mode.deform_1;
                    }
                }
                else if (mode == Mode.scale_1)
                {
                    if (inputType == Input.hold)
                    {
                        sculptureManager.StartScaling();
                        mode = Mode.scale_2;
                    }
                }
                else if (mode == Mode.scale_2)
                {
                    if (inputType == Input.none)
                    {
                        sculptureManager.StopScaling();
                        mode = Mode.scale_1;
                    }
                }
                else if (mode == Mode.rotate_1)
                {
                    if (inputType == Input.click)
                    {
                        sculptureManager.StartRotation();
                        mode = Mode.rotate_2;
                    }
                }
                else if (mode == Mode.rotate_2)
                {
                    if (inputType == Input.none)
                    {
                        sculptureManager.StopRotation();
                        mode = Mode.rotate_1;
                    }
                }
                
                if (ismenuopen)
                    hands = frame.GetHands(calibrationManager.GetProjectiveMatrix(menuManager.GetHeight(), 1000, 1000, Vector3.zero));
                else
                    hands = frame.GetHands(calibrationManager.GetProjectiveMatrix(sculptureManager.GetHeight()));
                UpdatePointer(hands, showpointer);
            }
            else
            {
                UpdatePointer(null, false);
            }
        }
    }

    void StartCalibration()
    {
        mode = Mode.calibrate;
        calibrationManager.StartCalibration();
    }

    void MenuAction(int m)
    {
        switch (m)
        {
            case 0:
                mode = Mode.split_1;
                target = Target.sculpture;
                CloseMenu();
                break;
            case 1:
                mode = Mode.extrude_1;
                target = Target.sculpture;
                CloseMenu();
                break;
            case 2:
                mode = Mode.deform_1;
                target = Target.sculpture;
                CloseMenu();
                break;
            case 3:
                mode = Mode.scale_1;
                target = Target.sculpture;
                CloseMenu();
                break;
            case 4:
                mode = Mode.rotate_1;
                target = Target.sculpture;
                CloseMenu();
                break;
            case 5:
                sculptureManager.Undo();
                break;
            case 6:
                sculptureManager.Redo();
                break;
            case 7:
                sculptureManager.SaveMesh();
                target = Target.sculpture;
                CloseMenu();
                break;
            default:
                target = Target.sculpture;
                CloseMenu();
                break;
        }

        ResetMode();
    }

    void ResetMode()
    {
        if (mode == Mode.split_2)
            mode = Mode.split_1;
        else if (mode == Mode.extrude_2)
            mode = Mode.extrude_1;
        else if (mode == Mode.scale_2)
            mode = Mode.scale_1;
        else if (mode == Mode.rotate_2)
            mode = Mode.rotate_1;

        firstselection = true;
    }

    void OpenMenu()
    {
        ismenuopen = true;
        sculptureManager.ResetSelection();
        menuManager.Enable();
    }

    void CloseMenu()
    {
        ismenuopen = false;
        menuManager.Disable();
    }

    protected abstract void InitPointer();

    protected abstract void UpdatePointer(LeapHand[] hands, bool visible);

    protected abstract void UpdateHoloPointer(Vector3 currpos);
}
