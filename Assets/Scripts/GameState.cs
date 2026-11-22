using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    public int CorruptedLifeShattered { get; private set; }
    public int ObstaclesShattered { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RecordShatter(Shatterable.ShatterCategory category)
    {
        if (category == Shatterable.ShatterCategory.CorruptedLife)
            CorruptedLifeShattered++;
        else
            ObstaclesShattered++;
    }
}
