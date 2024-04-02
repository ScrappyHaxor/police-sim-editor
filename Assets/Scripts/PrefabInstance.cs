using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabInstance : MonoBehaviour
{
    public int ID;

    private void Start()
    {
        ID = Editor.PrefabID++;
    }
}
