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
    public GameObject PausePanel;
    public TMP_Text ResumeText;
    public TMP_Text TitleBackText;

    public GameObject gameRoot;

    InputSystem_Actions input;

    enum State { Title, Playing, GameOver, Stop }
    State state = State.Title;

    bool transitioning = false;

    int pauseSelection = 0;

    readonly Color selectedColor = Color.red;
    readonly Color normalColor = Color.black;

    [Header("Game Over Production")]
    public Image GameOverProductionImage;

    [Tooltip("暗転フェードイン時間（0→1）")]
    public float fadeInDuration = 0.6f;

    [Tooltip("暗転が完全に見えた後の待ち時間")]
    public float afterFadeHoldSeconds = 1.0f;

    [Tooltip("GAME OVER表示時間")]
    public float gameOverShowDuration = 1.0f;

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
        Time.timeScale = 1f;
    }

    void Start()
    {
        InitOverlay();

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (gameRoot != null) gameRoot.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        state = State.Title;
    }

    void Update()
    {
        if (state != State.Playing) return;
        if (player == null || spawner == null) return;

        var enemies = spawner.GetSpawnedEnemies();
        float dieDistSq = dieDistance * dieDistance;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            if ((enemy.transform.position - player.position).sqrMagnitude <= dieDistSq)
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
            if (pauseSelection == 0) ResumeFromStop();
            else ShowTitle();
        }
    }

    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        if (state == State.Playing) EnterStop();
        else if (state == State.Stop) ResumeFromStop();
    }

    void OnTogglePauseSelection(InputAction.CallbackContext ctx)
    {
        if (state != State.Stop) return;

        pauseSelection = 1 - pauseSelection;
        UpdatePauseHighlight();
    }

    // ======================
    // State
    // ======================
    void StartGame()
    {
        transitioning = true;
        state = State.Playing;

        if (TitleImage != null) TitleImage.enabled = false;
        if (GameOverImage != null) GameOverImage.enabled = false;

        InitOverlay();

        if (gameRoot != null) gameRoot.SetActive(true);

        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        transitioning = false;
    }

    void EnterStop()
    {
        state = State.Stop;
        pauseSelection = 0;
        UpdatePauseHighlight();

        if (PausePanel != null) PausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    void ResumeFromStop()
    {
        state = State.Playing;
        if (PausePanel != null) PausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    void UpdatePauseHighlight()
    {
        if (ResumeText != null)
            ResumeText.color = pauseSelection == 0 ? selectedColor : normalColor;

        if (TitleBackText != null)
            TitleBackText.color = pauseSelection == 1 ? selectedColor : normalColor;
    }

    // ======================
    // GameOver
    // ======================
    void OnGameOver()
    {
        if (state != State.Playing) return;

        transitioning = true;
        state = State.GameOver;

        if (gameRoot != null) gameRoot.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        StartCoroutine(FadeInWaitDisableThenGameOver());
    }

    IEnumerator FadeInWaitDisableThenGameOver()
    {
        if (GameOverProductionImage == null)
        {
            StartCoroutine(GameOverSequence());
            yield break;
        }

        // フェードイン開始（見えてない → 見える）
        GameOverProductionImage.enabled = true;
        SetOverlayAlpha(0f);

        float elapsed = 0f;
        float dur = Mathf.Max(0.0001f, fadeInDuration);

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            SetOverlayAlpha(Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        // 完全に見えた状態
        SetOverlayAlpha(1f);

        // ★1秒待つ
        yield return new WaitForSecondsRealtime(afterFadeHoldSeconds);

        // ★暗転画像を消す
        GameOverProductionImage.enabled = false;

        // ★ゲームオーバー表示開始
        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        if (GameOverImage != null) GameOverImage.enabled = true;

        yield return new WaitForSecondsRealtime(gameOverShowDuration);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        state = State.Title;
        transitioning = false;
    }

    void ShowTitle()
    {
        state = State.Title;

        if (TitleImage != null) TitleImage.enabled = true;
        if (GameOverImage != null) GameOverImage.enabled = false;

        InitOverlay();
    }

    // ======================
    // Helpers
    // ======================
    void InitOverlay()
    {
        if (GameOverProductionImage == null) return;

        GameOverProductionImage.enabled = false;
        SetOverlayAlpha(0f);
    }

    void SetOverlayAlpha(float a)
    {
        var c = GameOverProductionImage.color;
        c.a = Mathf.Clamp01(a);
        GameOverProductionImage.color = c;
    }
}
