using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorAction
{
    public Vector3[] oldquads;
    public Vector3[] newquads;

    public EditorAction(Vector3[] oldquads, Vector3[] newquads)
    {
        this.oldquads = oldquads;
        this.newquads = newquads;
    }
}
