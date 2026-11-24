using UnityEngine;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public int pointsPerFruit = 10;
    public int pointsPerCombo = 50;
    public int bombPenalty = 25;
    public float gameDuration = 120f;

    [Header("Combo System")]
    public float comboTimeWindow = 2.0f;
    public int fruitsForCombo = 3;

    [Header("Difficulty Settings")]
    public DifficultySettings difficultySettings = new DifficultySettings();

    // État
    public int startLives { get; private set; } = 3; // Sera mis à jour selon la difficulté
    private int currentScore = 0;
    private int currentLives;
    private float gameTimeRemaining;
    private bool gameRunning = false;
    private int consecutiveFruits = 0;
    private float lastFruitTime = 0f;
    private int totalFruitsSliced = 0;
    private int totalBombsTouched = 0;

    private Coroutine gameTimerCoroutine;

    // Références
    private FruitSpawner fruitSpawner;
    private UIManager ui;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        fruitSpawner = FindFirstObjectByType<FruitSpawner>();
        ui = FindFirstObjectByType<UIManager>();

        if (ui == null) Debug.LogError("UIManager non trouvé dans la scène.");
        if (fruitSpawner == null) Debug.LogError("FruitSpawner non trouvé dans la scène.");

        ui?.InitializeUI(this);
    }

    // ==================== DIFFICULTÉ ====================
    public void SetDifficulty(DifficultySettings.Difficulty difficulty)
    {
        difficultySettings.currentDifficulty = difficulty;
        startLives = difficultySettings.GetLives();
        
        // Appliquer les paramètres au spawner
        if (fruitSpawner != null)
        {
            fruitSpawner.minSpawnInterval = difficultySettings.GetMinSpawnInterval();
            fruitSpawner.maxSpawnInterval = difficultySettings.GetMaxSpawnInterval();
            fruitSpawner.bombSpawnChance = difficultySettings.GetBombChance();
        }

        Debug.Log($"Difficulté définie: {difficultySettings.GetDifficultyName()} - Vies: {startLives}");
    }

    // ==================== CYCLE DE JEU ====================
    public void StartGame()
    {
        if (gameRunning) return;

        currentScore = 0;
        currentLives = startLives;
        gameTimeRemaining = gameDuration;
        gameRunning = true;

        consecutiveFruits = 0;
        totalFruitsSliced = 0;
        totalBombsTouched = 0;

        fruitSpawner?.StartSpawnSystem();

        if (gameTimerCoroutine != null) StopCoroutine(gameTimerCoroutine);
        gameTimerCoroutine = StartCoroutine(GameTimerCoroutine());

        ui?.ShowGameUI(currentScore, gameTimeRemaining, currentLives);
    }

    public void EndGame()
    {
        if (!gameRunning) return;
        gameRunning = false;

        if (gameTimerCoroutine != null) { StopCoroutine(gameTimerCoroutine); gameTimerCoroutine = null; }
        fruitSpawner?.StopSpawning();
        CleanupObjects();

        ui?.ShowGameOverPanel(currentScore, totalFruitsSliced, totalBombsTouched);
    }

    public void RestartGame()
    {
        CleanupObjects();
        ui?.ShowDifficultyPanel(); // Retour à la sélection de difficulté
    }

    public void GoToMenu()
    {
        CleanupObjects();
        gameRunning = false;
        if (gameTimerCoroutine != null) { StopCoroutine(gameTimerCoroutine); gameTimerCoroutine = null; }
        fruitSpawner?.StopSpawning();
        ui?.ShowStartMenu();
    }

    public void QuitGame()
    {
        if (gameRunning) EndGame();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== SCORE / FRUITS ====================
    public void OnFruitSliced(GameObject fruit)
    {
        if (!gameRunning) return;

        totalFruitsSliced++;

        float t = Time.time;
        if (t - lastFruitTime <= comboTimeWindow)
        {
            consecutiveFruits++;
            if (consecutiveFruits >= fruitsForCombo) AwardCombo();
        }
        else
        {
            consecutiveFruits = 1;
        }
        lastFruitTime = t;

        // Fruit bonus (Part 1) : ses points remplacent ceux d'un fruit normal.
        FruitController fc = fruit != null ? fruit.GetComponentInParent<FruitController>() : null;
        bool isBonus = fc != null && fc.isSpecial;
        AddScore(isBonus ? fc.pointsValue : pointsPerFruit);
        if (isBonus && AudioManager.Instance != null) AudioManager.Instance.PlayScoreBonus();
        ui?.UpdateCombo(consecutiveFruits);
    }

    public void OnBombTouched()
    {
        if (!gameRunning) return;

        totalBombsTouched++;
        AddScore(-bombPenalty);
        LoseLife();
        consecutiveFruits = 0;
        ui?.UpdateCombo(0);
    }

    public void OnFruitMissed(GameObject fruit)
    {
        if (!gameRunning) return;
        consecutiveFruits = 0;
        ui?.UpdateCombo(0);
    }

    private void AwardCombo()
    {
        AddScore(pointsPerCombo);
        consecutiveFruits = 0;
        ui?.UpdateCombo(0);
    }

    private IEnumerator GameTimerCoroutine()
    {
        while (gameRunning && gameTimeRemaining > 0f)
        {
            gameTimeRemaining -= Time.deltaTime;
            ui?.UpdateTime(gameTimeRemaining);

            if (gameTimeRemaining <= 10f) ui?.ShowTimeWarning(true);
            if (gameTimeRemaining <= 0f) break;

            yield return null;
        }
        EndGame();
    }

    private void CleanupObjects()
    {
        // Pooling (Part 2, Task 1) : retour au stock des instances ACTIVES seulement
        // (FindObjectsInactive.Exclude : le stock désactivé n'est pas touché).
        var fruits = FindObjectsByType<FruitController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var f in fruits) if (f != null) Retire(f.gameObject);

        var bombs = FindObjectsByType<BombController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var b in bombs) if (b != null) Retire(b.gameObject);

        // Morceaux de fruits découpés (Task 2)
        var pieces = FindObjectsByType<SlicedPiece>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in pieces) if (p != null) Retire(p.gameObject);
    }

    static void Retire(GameObject go)
    {
        var po = go.GetComponent<PooledObject>();
        if (po != null) po.ReturnToPool();
        else Destroy(go);
    }

    private void AddScore(int points)
    {
        currentScore = Mathf.Max(0, currentScore + points);
        ui?.UpdateScore(currentScore);
    }

    private void LoseLife()
    {
        currentLives--;
        ui?.UpdateLives(currentLives);

        if (currentLives <= 0) EndGame();
    }

    // Getters
    public bool IsGameRunning() => gameRunning;
    public int GetCurrentScore() => currentScore;
    public int GetCurrentLives() => currentLives;
    public float GetTimeRemaining() => gameTimeRemaining;
    public int GetTotalFruitsSliced() => totalFruitsSliced;
    public int GetTotalBombsTouched() => totalBombsTouched;
}