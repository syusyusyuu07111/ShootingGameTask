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

    // ======================
    // Player Damage Visual
    // ======================
    [Header("Player Damage Visual")]
    [Tooltip("未設定なら player から自動取得")]
    public SpriteRenderer playerRenderer;

    [Range(0f, 1f)]
    public float damageRedStrength = 0.75f;

    public float damageHold = 0.05f;
    public float damageFadeDuration = 0.5f;

    // ======================
    // Slow Motion
    // ======================
    [Header("Slow Motion")]
    public bool enableSlowMotion = true;
    public float slowMoDurationRealtime = 1.0f;

    [Range(0.01f, 1f)]
    public float slowMoTimeScale = 0.2f;

    // ======================
    // Disable Control (NEW)
    // ======================
    [Header("Player Control Scripts")]
    [Tooltip("未設定なら player から自動取得")]
    public PlayerController playerController;

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    public BulletController bulletController;

    [Header("Game Over Production")]
    public float gameOverShowDuration = 1.0f;

    Color initialPlayerColor;
    bool hasInitialPlayerColor = false;

    Coroutine gameOverRoutine;

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
        EnablePlayerControls();
    }

    void Start()
    {
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (gameRoot != null) gameRoot.SetActive(false);

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

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

        if (gameRoot != null) gameRoot.SetActive(true);

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

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

        // ★被弾した瞬間から移動・攻撃を停止
        DisablePlayerControls();

        if (PausePanel != null) PausePanel.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        if (gameOverRoutine != null) StopCoroutine(gameOverRoutine);
        gameOverRoutine = StartCoroutine(DeathFlow());
    }

    IEnumerator DeathFlow()
    {
        // 1) 全員スロー（実時間1秒）
        if (enableSlowMotion)
            yield return StartCoroutine(PlaySlowMotion());

        // 2) プレイヤー赤＋フェード
        if (playerRenderer != null)
            yield return StartCoroutine(PlayPlayerDamageFade(playerRenderer));

        // 3) ゲーム本体停止
        if (gameRoot != null) gameRoot.SetActive(false);

        // 4) GAME OVER 表示
        yield return StartCoroutine(GameOverSequence());
    }

    IEnumerator PlaySlowMotion()
    {
        float prevScale = Time.timeScale;
        float prevFixed = Time.fixedDeltaTime;

        float s = Mathf.Clamp(slowMoTimeScale, 0.01f, 1f);
        Time.timeScale = s;
        Time.fixedDeltaTime = prevFixed * s;

        float wait = Mathf.Max(0f, slowMoDurationRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        Time.timeScale = prevScale;
        Time.fixedDeltaTime = prevFixed;
    }

    IEnumerator PlayPlayerDamageFade(SpriteRenderer sr)
    {
        CacheInitialPlayerColorIfNeeded();

        Color baseColor = hasInitialPlayerColor ? initialPlayerColor : sr.color;

        Color redColor = Color.Lerp(baseColor, Color.red, damageRedStrength);
        redColor.a = baseColor.a;
        sr.color = redColor;

        if (damageHold > 0f)
            yield return new WaitForSecondsRealtime(damageHold);

        float dur = Mathf.Max(0.0001f, damageFadeDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            Color c = redColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, a);
            sr.color = c;

            yield return null;
        }

        Color end = sr.color;
        end.a = 0f;
        sr.color = end;
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

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();

        EnablePlayerControls();

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;
    }

    // ======================
    // Control ON/OFF
    // ======================
    void DisablePlayerControls()
    {
        if (playerController != null) playerController.ControlEnabled = false;
        if (bulletController != null) bulletController.ControlEnabled = false;
    }

    void EnablePlayerControls()
    {
        if (playerController != null) playerController.ControlEnabled = true;
        if (bulletController != null) bulletController.ControlEnabled = true;
    }

    // ======================
    // Auto Bind
    // ======================
    void AutoBindPlayerRefs()
    {
        if (playerRenderer == null && player != null)
            playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);

        if (playerController == null && player != null)
            playerController = player.GetComponentInChildren<PlayerController>(true);

        // BulletController は player 配下じゃない場合もあるので、まず player 配下 → ダメならシーン検索（1回だけ）
        if (bulletController == null)
        {
            if (player != null)
                bulletController = player.GetComponentInChildren<BulletController>(true);

            if (bulletController == null)
                bulletController = FindFirstObjectByType<BulletController>();
        }
    }

    // ======================
    // Helpers
    // ======================
    void CacheInitialPlayerColorIfNeeded()
    {
        if (hasInitialPlayerColor) return;
        if (playerRenderer == null) return;

        initialPlayerColor = playerRenderer.color;
        hasInitialPlayerColor = true;
    }

    void ResetPlayerVisual()
    {
        if (playerRenderer == null) return;

        Color c = hasInitialPlayerColor ? initialPlayerColor : playerRenderer.color;
        if (c.a <= 0f) c.a = 1f;
        playerRenderer.color = c;
        playerRenderer.enabled = true;
    }
}
