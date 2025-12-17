using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using TMPro;

public class PlayerDie : MonoBehaviour
{
    public Transform player;
    public EnemySpawner spawner;

    public float dieDistance = 1.0f;

    public Image GameOverImage;
    public Image TitleImage;

    [Header("Pause UI")]
    public GameObject PausePanel;      // ポーズ画面Panel
    public TMP_Text ResumeText;        // 「ゲームに戻る」
    public TMP_Text TitleBackText;     // 「タイトルに戻る」

    public GameObject gameRoot;        // ゲーム中オブジェクトの親

    InputSystem_Actions input;

    enum State { Title, Playing, GameOver, Stop }
    State state = State.Title;

    bool transitioning = false;

    // 0 = Resume / 1 = Title
    int pauseSelection = 0;

    // 色指定
    readonly Color selectedColor = Color.red;
    readonly Color normalColor = Color.black;

    void Awake()
    {
        input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        input.UI.Enable();

        input.UI.Submit.performed += OnSubmit;
        input.UI.GameStop.performed += OnGameStop;
        input.UI.UpButton.performed += OnTogglePauseSelection;
        input.UI.DownButton.performed += OnTogglePauseSelection;
    }

    void OnDisable()
    {
        input.UI.Submit.performed -= OnSubmit;
        input.UI.GameStop.performed -= OnGameStop;
        input.UI.UpButton.performed -= OnTogglePauseSelection;
        input.UI.DownButton.performed -= OnTogglePauseSelection;

        input.UI.Disable();

        // 念のため
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        if (PausePanel != null) PausePanel.SetActive(false);

        if (gameRoot != null) gameRoot.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        Time.timeScale = 1f;
        state = State.Title;
    }

    void Update()
    {
        if (state != State.Playing) return;
        if (player == null || spawner == null) return;

        var enemies = spawner.GetSpawnedEnemies();
        Vector3 playerPos = player.position;
        float dieDistSq = dieDistance * dieDistance;

        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemyGO = enemies[i];
            if (enemyGO == null) continue;

            if ((enemyGO.transform.position - playerPos).sqrMagnitude <= dieDistSq)
            {
                OnGameOver();
                break;
            }
        }
    }

    // ======================
    // Input
    // ======================
    void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        if (state == State.Title)
        {
            StartGame();
            return;
        }

        if (state == State.GameOver)
        {
            ShowTitle();
            return;
        }

        if (state == State.Stop)
        {
            if (pauseSelection == 0)
                ResumeFromStop();
            else
                ShowTitle();
        }
    }

    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        if (state == State.Playing)
            EnterStop();
        else if (state == State.Stop)
            ResumeFromStop();
    }

    void OnTogglePauseSelection(InputAction.CallbackContext ctx)
    {
        if (state != State.Stop) return;

        pauseSelection = 1 - pauseSelection; // 0⇔1
        UpdatePauseHighlight();
    }

    // ======================
    // State transitions
    // ======================
    void StartGame()
    {
        transitioning = true;
        state = State.Playing;

        Time.timeScale = 1f;

        if (TitleImage != null) TitleImage.enabled = false;
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (PausePanel != null) PausePanel.SetActive(false);

        if (gameRoot != null) gameRoot.SetActive(true);

        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        transitioning = false;
        Debug.Log("[State] StartGame");
    }

    void EnterStop()
    {
        state = State.Stop;

        pauseSelection = 0; // 初期は「ゲームに戻る」
        UpdatePauseHighlight();

        if (PausePanel != null) PausePanel.SetActive(true);

        Time.timeScale = 0f;

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        Debug.Log("[State] Stop (Paused)");
    }

    void ResumeFromStop()
    {
        state = State.Playing;

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        Debug.Log("[State] Resume (Unpaused)");
    }

    void UpdatePauseHighlight()
    {
        if (ResumeText != null)
            ResumeText.color = (pauseSelection == 0) ? selectedColor : normalColor;

        if (TitleBackText != null)
            TitleBackText.color = (pauseSelection == 1) ? selectedColor : normalColor;
    }

    void OnGameOver()
    {
        if (state != State.Playing) return;

        transitioning = true;
        state = State.GameOver;

        Time.timeScale = 1f;

        if (PausePanel != null) PausePanel.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        if (gameRoot != null) gameRoot.SetActive(false);

        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        if (GameOverImage != null) GameOverImage.enabled = true;

        yield return new WaitForSecondsRealtime(1f);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        transitioning = false;
        Debug.Log("[State] GameOver -> Title");
    }

    void ShowTitle()
    {
        transitioning = true;
        state = State.Title;

        Time.timeScale = 1f;

        if (TitleImage != null) TitleImage.enabled = true;
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (PausePanel != null) PausePanel.SetActive(false);

        if (gameRoot != null) gameRoot.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        transitioning = false;
        Debug.Log("[State] ShowTitle");
    }
}
