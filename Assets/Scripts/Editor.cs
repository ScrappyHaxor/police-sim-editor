using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;
using UnityEditor.SceneManagement;
using UnityEngine;

public enum EditorMode
{
    STRUCTURE,
    FURNITURE
}

public enum StructureType
{
    MIDDLE,
    WALL,
    CORNER,
    DOOR,
    WINDOW,
    TWO_WALL,
    INNER_DOOR,
    INNER_WALL,
    INNER_CORNER
}

public enum FurnitureType
{
    SOFA,
    TABLE,
    CHAIR,
    FRIDGE,
    CABINET1,
    CABINET2,
    WALL_WINE,
    BAR_CABINET,
    TV,
    BED_SMALL,
    END_TABLE,
    DRESSER,
    BED_BIG,
    TABLE_LAMP,
    BATHROOM_COUNTER,
    BATH_TUB,
    HAMPER,
    MIRROR,
    OFFICE_CHAIR,
    RUG,
    COMPUTER_DESK,
    STOVE,
    COFFEE_TABLE
}

public struct PrefabContainer<T> where T : struct, IConvertible
{
    public GameObject prefabObject;
    public T type;

    public PrefabContainer(GameObject prefabObject, T type)
    {
        this.prefabObject = prefabObject;
        this.type = type;
    }
}

public class DoorNode
{
    public GameObject Door;
    public DoorNode Next;
}

public class PrefabCatalogue<T> where T : struct, IConvertible
{
    private List<GameObject> prefabs;
    private int currentIndex;
    private T currentType;

    public PrefabCatalogue(List<GameObject> prefabs)
    {
        if (!typeof(T).IsEnum)
        {
            throw new ArgumentException("T must be an enum.");
        }

        this.prefabs = prefabs;
        currentIndex = 0;
        currentType = default;
    }

    public void CycleUp()
    {
        currentIndex++;
        if (currentIndex > prefabs.Count - 1)
            currentIndex = 0;

        currentType = (T)(object)currentIndex;
    }

    public void CycleDown()
    {
        currentIndex--;
        if (currentIndex < 0)
            currentIndex = prefabs.Count - 1;

        currentType = (T)(object)currentIndex;
    }

    public GameObject GetSelected()
    {
        return prefabs[currentIndex];
    }

    public T GetSelectedType()
    {
        return currentType;
    }

}

public class Editor : MonoBehaviour
{
    public EditorMode mode;

    public List<GameObject> structurePrefabs;
    public List<GameObject> furniturePrefabs;

    private PrefabCatalogue<StructureType> structures;
    private PrefabCatalogue<FurnitureType> furniture;

    public float cameraSpeed;
    public Camera editorCamera;


    public int Snapping;

    private Dictionary<int, PrefabContainer<StructureType>> structurePositions;
    private Dictionary<int, PrefabContainer<FurnitureType>> furniturePositions;

    private Vector3 startPosition;

    private GameObject ghost;

    private float horizontal;
    private float vertical;
    private float rotation;
    private float rotationIncrement;

    private Plane hitscanPlane;

    public GameObject temp;

    private bool[,] graph;
    private int[,] graphHashes;

    // Start is called before the first frame update
    void Start()
    {
        mode = EditorMode.STRUCTURE;

        structures = new PrefabCatalogue<StructureType>(structurePrefabs);
        furniture = new PrefabCatalogue<FurnitureType>(furniturePrefabs);

        structurePositions = new Dictionary<int, PrefabContainer<StructureType>>();
        furniturePositions = new Dictionary<int, PrefabContainer<FurnitureType>>();

        hitscanPlane = new Plane(Vector2.down, 0f);

        rotationIncrement = 90;

        UpdatePrefabGhost();
    }

    void UpdatePrefabGhost()
    {
        GameObject newGhost = null;
        
        switch (mode)
        {
            case EditorMode.STRUCTURE:
                newGhost = Instantiate(structures.GetSelected(), transform);
                break;
            case EditorMode.FURNITURE:
                newGhost = Instantiate(furniture.GetSelected(), transform);
                break;
            default:
                newGhost = null;
                break;
        }
        

        if (newGhost != null && ghost != null)
        {
            newGhost.transform.rotation = ghost.transform.rotation;
            Destroy(ghost);
        }

        ghost = newGhost;
    }

    public static int HashableVector(Vector3 vector)
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
        Vector2 minPosition = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 maxPosition = new Vector2(float.MinValue, float.MinValue);

        foreach (KeyValuePair<int, PrefabContainer<StructureType>> pair in structurePositions)
        {
            if (pair.Value.prefabObject.IsDestroyed())
                continue;

            GameObject prefab = pair.Value.prefabObject;
            Vector2 convertedPos = new Vector2(prefab.transform.position.x, prefab.transform.position.z);
            if (convertedPos.x <= minPosition.x)
            {
                minPosition.x = convertedPos.x;
            }

            if (convertedPos.y <= minPosition.y)
            {
                minPosition.y = convertedPos.y;
            }

            if (convertedPos.x >= maxPosition.x)
            {
                maxPosition.x = convertedPos.x;
            }

            if (convertedPos.y >= maxPosition.y)
            {
                maxPosition.y = convertedPos.y;
            }
        }

        Vector2 diff = maxPosition - minPosition;
        diff /= 5;
        diff = new Vector2(Mathf.Round(diff.x), Mathf.Round(diff.y));

        graph = new bool[(int)diff.x + 1, (int)diff.y + 1];
        graphHashes = new int[(int)diff.x + 1, (int)diff.y + 1];
        for (float x = minPosition.x; x <= maxPosition.x; x += 5)
        {
            for (float y = maxPosition.y; y >= minPosition.y; y -= 5)
            {
                float correctedX = x + Mathf.Abs(minPosition.x);
                float correctedY = y + Mathf.Abs(minPosition.y);
                correctedX /= 5.0f;
                correctedY = MathF.Abs((correctedY / 5.0f) - diff.y);

                int hash = HashableVector(new Vector3(x, 0, y));
                if (structurePositions.ContainsKey(hash))
                {
                    graph[(int)correctedX, (int)correctedY] = true;
                }
                else
                {
                    graph[(int)correctedX, (int)correctedY] = false;
                }

                graphHashes[(int)correctedX, (int)correctedY] = HashableVector(new Vector3(x, 0, y));

                GameObject testCube = Instantiate(temp);
                testCube.transform.position = new Vector3(x, 0, y);

                if (graph[(int)correctedX, (int)correctedY])
                {
                    testCube.GetComponent<MeshRenderer>().material.color = Color.green;
                }
                else
                {
                    testCube.GetComponent<MeshRenderer>().material.color = Color.red;
                }
            }
        }
    }

    private void SaveLevel()
    {
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;

        XmlWriter writer = XmlWriter.Create("map.xml", settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("map");

        writer.WriteStartElement("Start");
        writer.WriteElementString("X", startPosition.x.ToString());
        writer.WriteElementString("Y", startPosition.y.ToString());
        writer.WriteElementString("Z", startPosition.z.ToString());
        writer.WriteEndElement();

        writer.WriteStartElement("Structures");
        foreach (KeyValuePair<int, PrefabContainer<StructureType>> prefab in structurePositions)
        {
            if (prefab.Value.prefabObject.IsDestroyed())
                continue;

            writer.WriteStartElement("Structure");
            writer.WriteElementString("X", prefab.Value.prefabObject.transform.position.x.ToString());
            writer.WriteElementString("Y", prefab.Value.prefabObject.transform.position.y.ToString());
            writer.WriteElementString("Z", prefab.Value.prefabObject.transform.position.z.ToString());
            writer.WriteElementString("Rotation", prefab.Value.prefabObject.transform.rotation.eulerAngles.y.ToString());
            writer.WriteElementString("Type", prefab.Value.type.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("Furniture");
        foreach (KeyValuePair<int, PrefabContainer<FurnitureType>> prefab in furniturePositions)
        {
            if (prefab.Value.prefabObject.IsDestroyed())
                continue;

            writer.WriteStartElement("Furniture");
            writer.WriteElementString("X", prefab.Value.prefabObject.transform.position.x.ToString());
            writer.WriteElementString("Y", prefab.Value.prefabObject.transform.position.y.ToString());
            writer.WriteElementString("Z", prefab.Value.prefabObject.transform.position.z.ToString());
            writer.WriteElementString("Rotation", prefab.Value.prefabObject.transform.rotation.eulerAngles.y.ToString());
            writer.WriteElementString("Type", prefab.Value.type.ToString());
            writer.WriteEndElement();
        }
        writer.WriteEndElement();

        writer.WriteStartElement("Graph");
        writer.WriteElementString("Width", graph.GetLength(0).ToString());
        writer.WriteElementString("Height", graph.GetLength(1).ToString());
        writer.WriteStartElement("Nodes");
        for (int x = 0; x < graph.GetLength(0); x++)
        {
            for (int y = 0; y < graph.GetLength(1); y++)
            {
                writer.WriteStartElement("Node");
                writer.WriteElementString("X", x.ToString());
                writer.WriteElementString("Y", y.ToString());
                writer.WriteElementString("RelatedHash", graphHashes[x, y].ToString());
                writer.WriteElementString("Passable", graph[x, y].ToString());
                writer.WriteEndElement();
            }
        }
        writer.WriteEndElement();
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
            success = TryParseEnum(reader.ReadElementContentAsString(), out StructureType Type);
            if (!success)
                return false;

            GameObject instance = Instantiate(structurePrefabs[(int)Type]);
            instance.transform.position = new Vector3(X, Y, Z);
            instance.transform.rotation = Quaternion.Euler(0, Rotation, 0);

            structurePositions.Add(HashableVector(new Vector3(X, Y, Z)), new PrefabContainer<StructureType>(instance, Type));
        }

        reader.Close();

        return true;
    }

    private void ClearLevel()
    {
        foreach (KeyValuePair<int, PrefabContainer<StructureType>> prefab in structurePositions)
        {
            Destroy(prefab.Value.prefabObject);
        }

        foreach (KeyValuePair<int, PrefabContainer<FurnitureType>> prefab in furniturePositions)
        {
            Destroy(prefab.Value.prefabObject);
        }

        structurePositions.Clear();
        furniturePositions.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        horizontal = Input.GetAxis("Horizontal");
        vertical = Input.GetAxis("Vertical");

        if (Input.GetKeyDown(KeyCode.R))
        {
            switch (mode)
            {
                case EditorMode.STRUCTURE:
                    structures.CycleDown();
                    break;
                case EditorMode.FURNITURE:
                    furniture.CycleDown();
                    break;
            }
            UpdatePrefabGhost();
        }
        else if (Input.GetKeyDown(KeyCode.T))
        {
            switch (mode)
            {
                case EditorMode.STRUCTURE:
                    structures.CycleUp();
                    break;
                case EditorMode.FURNITURE:
                    furniture.CycleUp();
                    break;
            }
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

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            mode++;
            if ((int)mode > Enum.GetValues(typeof(EditorMode)).Cast<int>().Max())
                mode = 0;

            switch (mode)
            {
                case EditorMode.STRUCTURE:
                    rotationIncrement = 90;
                    break;
                case EditorMode.FURNITURE:
                    rotationIncrement = 45;
                    break;
            }

            UpdatePrefabGhost();
            rotation = 0;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {

        }

        if (Input.GetKeyDown(KeyCode.Q))
            rotation -= rotationIncrement;
        else if (Input.GetKeyDown(KeyCode.E))
            rotation += rotationIncrement;

        if (horizontal != 0 || vertical != 0)
            editorCamera.transform.position += cameraSpeed * Time.deltaTime * new Vector3(horizontal, 0, vertical);

        Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
        hitscanPlane.Raycast(ray, out float distance);
        Vector3 rayPosition = ray.GetPoint(distance);

        switch (mode)
        {
            case EditorMode.STRUCTURE:
                ghost.transform.position = new Vector3(MathF.Round(rayPosition.x / 5) * 5, 0, MathF.Round(rayPosition.z / 5) * 5);
                break;
            case EditorMode.FURNITURE:
                ghost.transform.position = new Vector3(MathF.Round(rayPosition.x / 0.625f) * 0.625f, 0, MathF.Round(rayPosition.z / 0.625f) * 0.625f);
                break;
        }

        
        ghost.transform.Rotate(0, rotation, 0);

        Vector3 ghostPosition = new Vector3(ghost.transform.position.x, 0, ghost.transform.position.z);
        int ghostHash = HashableVector(ghostPosition);

        if (Input.GetKeyDown(KeyCode.M))
        {
            startPosition = new Vector3(ghostPosition.x, 0, ghostPosition.z);
        }

        if (mode == EditorMode.STRUCTURE)
        {
            if (Input.GetButton("Fire1") && !structurePositions.ContainsKey(ghostHash))
            {
                GameObject instance = Instantiate(structures.GetSelected());
                instance.transform.position = ghostPosition;
                instance.transform.rotation = ghost.transform.rotation;
                structurePositions.Add(ghostHash, new PrefabContainer<StructureType>(instance, structures.GetSelectedType()));
            }

            if (Input.GetButtonDown("Fire2") && structurePositions.ContainsKey(ghostHash))
            {
                GameObject instance = structurePositions[ghostHash].prefabObject;
                Destroy(instance);
                structurePositions.Remove(ghostHash);
            }
        }
        else if (mode == EditorMode.FURNITURE)
        {
            if (Input.GetButton("Fire1") && !furniturePositions.ContainsKey(ghostHash))
            {
                GameObject instance = Instantiate(furniture.GetSelected());
                instance.transform.position = ghostPosition;
                instance.transform.rotation = ghost.transform.rotation;
                furniturePositions.Add(ghostHash, new PrefabContainer<FurnitureType>(instance, furniture.GetSelectedType()));
            }

            if (Input.GetButtonDown("Fire2") && furniturePositions.ContainsKey(ghostHash))
            {
                GameObject instance = furniturePositions[ghostHash].prefabObject;
                Destroy(instance);
                furniturePositions.Remove(ghostHash);
            }
        }

        rotation = 0;


    }

}
