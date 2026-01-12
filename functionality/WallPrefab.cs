using UnityEngine;

public class WallPlacer : MonoBehaviour
{
    public GameObject wallPrefab;
    public int wallCount = 10;
    public float wallSpacing = 2.0f;

    void Start()
    {
        for (int i = 0; i < wallCount; i++)
        {
            Vector3 position = new Vector3(i * wallSpacing, 0, 0);
            Instantiate(wallPrefab, position, Quaternion.identity);
        }
    }
}
