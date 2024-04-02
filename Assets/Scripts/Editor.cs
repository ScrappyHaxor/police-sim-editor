using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Editor : MonoBehaviour
{
    public float cameraSpeed;
    public Camera editorCamera;
    public List<GameObject> prefabs;

    public int Snapping;

    public static int PrefabID;

    private List<GameObject> instantiatedPrefabs;

    private int currentPrefabIndex;

    private GameObject ghost;

    private float horizontal;
    private float vertical;
    private float rotation;

    private Plane hitscanPlane;

    private bool collisionDetected;
    public int collisions;

    // Start is called before the first frame update
    void Start()
    {
        instantiatedPrefabs = new List<GameObject>();

        hitscanPlane = new Plane(Vector2.down, 0f);

        UpdatePrefabGhost();
    }

    void OnCollisionEnter(Collision collision)
    {
        PrefabInstance ghostInstance = ghost.GetComponentInParent<PrefabInstance>();
        PrefabInstance instance = collision.gameObject.GetComponentInParent<PrefabInstance>();
        if (instance != null && ghostInstance.ID == instance.ID)
            return;

        collisionDetected = true;
        collisions++;
    }

    private void OnCollisionExit(Collision collision)
    {
        PrefabInstance ghostInstance = ghost.GetComponentInParent<PrefabInstance>();
        PrefabInstance instance = collision.gameObject.GetComponentInParent<PrefabInstance>();
        if (instance != null && ghostInstance.ID == instance.ID)
            return;

        collisions--;
        if (collisions == 0)
            collisionDetected = false;
    }

    void UpdatePrefabGhost()
    {
        if (ghost != null)
            Destroy(ghost);

        ghost = Instantiate(prefabs[currentPrefabIndex], transform);
        collisions = 0;
        collisionDetected = false;
    }

    // Update is called once per frame
    void Update()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");

        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentPrefabIndex--;
            if (currentPrefabIndex < 0)
                currentPrefabIndex = prefabs.Count - 1;
            UpdatePrefabGhost();
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentPrefabIndex++;
            if (currentPrefabIndex > prefabs.Count - 1)
                currentPrefabIndex = 0;
            UpdatePrefabGhost();
        }

        if (Input.GetKeyDown(KeyCode.C))
            rotation -= 90;
        else if (Input.GetKeyDown(KeyCode.V))
            rotation += 90;

        Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
        hitscanPlane.Raycast(ray, out float distance);
        Vector3 rayPosition = ray.GetPoint(distance);
        ghost.transform.position = new Vector3(MathF.Round(rayPosition.x), rayPosition.y, MathF.Round(rayPosition.z));
        ghost.transform.Rotate(0, rotation, 0);

        if (horizontal != 0 || vertical != 0)
            editorCamera.transform.position += cameraSpeed * Time.deltaTime * new Vector3(horizontal, 0, vertical);

        if (Input.GetButtonDown("Fire1") && !collisionDetected)
        {
            GameObject instance = Instantiate(prefabs[currentPrefabIndex]);
            instance.transform.position = ghost.transform.position;
            instance.transform.rotation = ghost.transform.rotation;
            instantiatedPrefabs.Add(instance);
        }

        rotation = 0;
    }

}
