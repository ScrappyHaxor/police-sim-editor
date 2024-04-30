using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;

public enum PrefabType
{
    MIDDLE,
    WALL,
    CORNER,
    DOOR,
    WINDOW
}

public struct PrefabContainer
{
    public GameObject prefabObject;
    public PrefabType prefabType;

    public PrefabContainer(GameObject prefabObject, PrefabType prefabType)
    {
        this.prefabObject = prefabObject;
        this.prefabType = prefabType;
    }
}

public class Editor : MonoBehaviour
{
    public float cameraSpeed;
    public Camera editorCamera;
    public List<GameObject> prefabs;

    public int Snapping;

    public static int PrefabID;

    private Dictionary<int, PrefabContainer> takenPositions;

    private int currentPrefabIndex;
    private PrefabType currentPrefabType;

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
        takenPositions = new Dictionary<int, PrefabContainer>();

        hitscanPlane = new Plane(Vector2.down, 0f);

        currentPrefabIndex = 0;

        currentPrefabType = (PrefabType)currentPrefabIndex;

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

    public static int HashableInt(Vector3 vector)
    {
        int x = Mathf.RoundToInt(vector.x);
        int y = Mathf.RoundToInt(vector.y);
        int z = Mathf.RoundToInt(vector.z);
        return x * 1000 + z + y * 1000000;
    }

    public static bool TryParseEnum<T>(string value, out T result)
    {
        result = default;
        bool success = Enum.IsDefined(typeof(T), value);
        if (success)
            result = (T)Enum.Parse(typeof(T), value);

        return success;
    }

    private void CalculateZones()
    { 
        foreach (KeyValuePair<int, PrefabContainer> prefab in takenPositions)
        {

        }
    }

    private void SaveLevel()
    {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;

        XmlWriter writer = XmlWriter.Create("map.xml", settings);
        writer.WriteStartDocument();
        writer.WriteStartElement(Path.GetFileNameWithoutExtension(name));

        writer.WriteStartElement("Prefabs");
        foreach (KeyValuePair<int, PrefabContainer> prefab in takenPositions)
        {
            writer.WriteStartElement("Prefab");
            writer.WriteElementString("X", prefab.Value.prefabObject.transform.position.x.ToString());
            writer.WriteElementString("Y", prefab.Value.prefabObject.transform.position.y.ToString());
            writer.WriteElementString("Z", prefab.Value.prefabObject.transform.position.z.ToString());
            writer.WriteElementString("Rotation", prefab.Value.prefabObject.transform.rotation.eulerAngles.y.ToString());
            writer.WriteElementString("Type", prefab.Value.prefabType.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        writer.Close();
    }

    private bool LoadLevel()
    {
        ClearLevel();
        XmlReaderSettings settings = new XmlReaderSettings();

        XmlReader reader;

        try
        {
            reader = XmlReader.Create("map.xml", settings);
        }
        catch (FileNotFoundException)
        {
            return false;
        }

        while (reader.ReadToFollowing("Prefab"))
        {
            reader.ReadToFollowing("X");
            bool success = float.TryParse(reader.ReadElementContentAsString(), out float X);
            if (!success)
                return false;

            reader.ReadToFollowing("Y");
            success = float.TryParse(reader.ReadElementContentAsString(), out float Y);
            if (!success)
                return false;

            reader.ReadToFollowing("Z");
            success = float.TryParse(reader.ReadElementContentAsString(), out float Z);
            if (!success)
                return false;

            reader.ReadToFollowing("Rotation");
            success = float.TryParse(reader.ReadElementContentAsString(), out float Rotation);
            if (!success)
                return false;

            reader.ReadToFollowing("Type");
            success = TryParseEnum(reader.ReadElementContentAsString(), out PrefabType Type);
            if (!success)
                return false;

            GameObject instance = Instantiate(prefabs[(int)Type]);
            instance.transform.position = new Vector3(X, Y, Z);
            instance.transform.rotation = Quaternion.Euler(0, Rotation, 0);

            takenPositions.Add(HashableInt(new Vector3(X, Y, Z)), new PrefabContainer(instance, Type));
        }

        reader.Close();

        return true;
    }

    private void ClearLevel()
    {
        foreach (KeyValuePair<int, PrefabContainer> prefab in takenPositions)
        {
            Destroy(prefab.Value.prefabObject);
        }
        takenPositions.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");

        if (Input.GetKeyDown(KeyCode.R))
        {
            currentPrefabIndex--;
            if (currentPrefabIndex < 0)
                currentPrefabIndex = prefabs.Count - 1;

            currentPrefabType = (PrefabType)currentPrefabIndex;

            UpdatePrefabGhost();
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            currentPrefabIndex++;
            if (currentPrefabIndex > prefabs.Count - 1)
                currentPrefabIndex = 0;

            currentPrefabType = (PrefabType)currentPrefabIndex;

            UpdatePrefabGhost();
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            CalculateZones();
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.S))
        {
            SaveLevel();
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
        {
            LoadLevel();
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.X))
        {
            ClearLevel();
        }

        if (Input.GetKeyDown(KeyCode.Q))
            rotation -= 90;
        else if (Input.GetKeyDown(KeyCode.E))
            rotation += 90;

        Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
        hitscanPlane.Raycast(ray, out float distance);
        Vector3 rayPosition = ray.GetPoint(distance);
        ghost.transform.position = new Vector3(MathF.Round(rayPosition.x / 5) * 5, rayPosition.y, MathF.Round(rayPosition.z / 5) * 5);
        ghost.transform.Rotate(0, rotation, 0);

        if (horizontal != 0 || vertical != 0)
            editorCamera.transform.position += cameraSpeed * Time.deltaTime * new Vector3(horizontal, 0, vertical);

        if (Input.GetButtonDown("Fire1") && !takenPositions.ContainsKey(HashableInt(ghost.transform.position)))
        {
            GameObject instance = Instantiate(prefabs[currentPrefabIndex]);
            instance.transform.position = ghost.transform.position;
            instance.transform.rotation = ghost.transform.rotation;
            takenPositions.Add(HashableInt(instance.transform.position), new PrefabContainer(instance, currentPrefabType));
        }

        if (Input.GetButtonDown("Fire2") && takenPositions.ContainsKey(HashableInt(ghost.transform.position)))
        {
            GameObject instance = takenPositions[HashableInt(ghost.transform.position)].prefabObject;
            Destroy(instance);
            takenPositions.Remove(HashableInt(ghost.transform.position));
        }

        rotation = 0;
    }

}
