using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using TMPro;
using System.Linq;

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
    // BGM
    // ======================
    [Header("BGM")]
    [Tooltip("未設定ならシーンから自動取得")]
    public BGMManager bgm;

    // ======================
    // SE (One Shot)
    // ======================
    [Header("SE (One Shot)")]
    [Tooltip("未設定ならこのオブジェクトから自動取得")]
    public AudioSource seSource;

    [Header("SE Clips")]
    [Tooltip("タイトル→ゲーム開始の瞬間に鳴らすSE")]
    public AudioClip startGameOneShot;

    [Range(0f, 1f)]
    public float startGameOneShotVolume = 1.0f;

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE")]
    public AudioClip hitOneShot;

    [Range(0f, 1f)]
    public float hitOneShotVolume = 1.0f;

    // （任意）多重防止
    [Header("SE Limiter (Optional)")]
    [Tooltip("この秒数以内の連続再生は無視（連打防止）")]
    public float seMinInterval = 0.03f;

    float lastSePlayTime = -999f;

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
    // Disable Control
    // ======================
    [Header("Player Control Scripts")]
    [Tooltip("未設定なら player から自動取得")]
    public PlayerController playerController;

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    public BulletController bulletController;

    [Header("Game Over Production")]
    public float gameOverShowDuration = 3.0f;

    // ======================
    // Player Reset
    // ======================
    [Header("Player Reset")]
    [Tooltip("タイトルに戻ったとき、プレイヤー位置をここに戻す。未設定なら Start 時の位置を使う")]
    public Transform playerSpawnPoint;

    [Tooltip("StartGame 時にもリセットする（安全）")]
    public bool resetPositionOnStartGame = true;

    Vector3 initialPlayerPosition;
    bool hasInitialPlayerPosition = false;

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

        // BGM自動取得
        if (bgm == null)
            bgm = FindFirstObjectByType<BGMManager>();

        // SE Source 自動取得
        if (seSource == null)
            seSource = GetComponent<AudioSource>();
        if (seSource != null)
            seSource.playOnAwake = false;

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

        CacheInitialPlayerPositionIfNeeded();
        ResetPlayerPosition();

        // タイトル初期化として「敵スポーン状態」を完全に戻す
        ResetSpawnerAndEnemies();

        state = State.Title;

        // タイトルBGM
        if (bgm != null) bgm.PlayTitle();
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
        // ★タイトル→ゲーム開始の瞬間にSE（最優先で鳴らす）
        PlayStartGameOneShot();

        transitioning = true;
        state = State.Playing;

        if (TitleImage != null) TitleImage.enabled = false;
        if (GameOverImage != null) GameOverImage.enabled = false;

        if (gameRoot != null) gameRoot.SetActive(true);

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

        CacheInitialPlayerPositionIfNeeded();
        if (resetPositionOnStartGame) ResetPlayerPosition();

        // 開始前に「敵スポーン状態」を完全に戻す
        ResetSpawnerAndEnemies();

        // Spawner開始
        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        transitioning = false;

        // ゲームBGM
        if (bgm != null) bgm.PlayGame();
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

        // 被弾した瞬間に「インゲームBGMを即停止」→SEを最優先で鳴らす
        if (bgm != null) bgm.StopBgmImmediate();
        PlayHitOneShot();

        // その直後から移動・攻撃を停止
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

    // ======================
    // SE Play
    // ======================
    void PlayStartGameOneShot()
    {
        if (startGameOneShot == null) return;

        if (Time.time - lastSePlayTime < seMinInterval)
            return;
        lastSePlayTime = Time.time;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(startGameOneShot, transform.position, startGameOneShotVolume);
            return;
        }

        seSource.PlayOneShot(startGameOneShot, startGameOneShotVolume);
    }

    void PlayHitOneShot()
    {
        if (hitOneShot == null) return;

        if (Time.time - lastSePlayTime < seMinInterval)
            return;
        lastSePlayTime = Time.time;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(hitOneShot, player != null ? player.position : Vector3.zero, hitOneShotVolume);
            return;
        }

        seSource.PlayOneShot(hitOneShot, hitOneShotVolume);
    }

    IEnumerator DeathFlow()
    {
        // 1) 全員スロー（実時間1秒）
        if (enableSlowMotion)
            yield return StartCoroutine(PlaySlowMotion());

        // 2) プレイヤー赤＋フェード
        if (playerRenderer != null)
            yield return StartCoroutine(PlayPlayerDamageFade(playerRenderer));

        // ★3) 演出の後でゲームオーバーBGM（SEを邪魔しない）
        if (bgm != null) bgm.PlayGameOver();

        // 4) ゲーム本体停止
        if (gameRoot != null) gameRoot.SetActive(false);

        // 5) GAME OVER 表示
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

        // タイトルに戻る瞬間に「位置」と「敵スポーン状態」をリセット
        ResetPlayerPosition();
        ResetSpawnerAndEnemies();

        // タイトルBGM
        if (bgm != null) bgm.PlayTitle();
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

        CacheInitialPlayerPositionIfNeeded();
        ResetPlayerPosition();

        // 敵スポーン状態も最初に戻す
        ResetSpawnerAndEnemies();

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        if (bgm != null) bgm.PlayTitle();
    }

    // ======================
    // Enemy Reset
    // ======================
    void ResetSpawnerAndEnemies()
    {
        if (spawner == null) return;

        // スポーン停止＆Spawner無効化
        spawner.StopSpawn();
        spawner.enabled = false;

        var enemies = spawner.GetSpawnedEnemies();
        if (enemies == null) return;

        // foreach中にリストが変わる可能性があるのでスナップショットで消す
        var snapshot = enemies.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            var e = snapshot[i];
            if (e == null) continue;
            Destroy(e);
        }
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

        if (bulletController == null)
        {
            if (player != null)
                bulletController = player.GetComponentInChildren<BulletController>(true);

            if (bulletController == null)
                bulletController = FindFirstObjectByType<BulletController>();
        }
    }

    // ======================
    // Player Position Reset
    // ======================
    void CacheInitialPlayerPositionIfNeeded()
    {
        if (hasInitialPlayerPosition) return;
        if (player == null) return;

        initialPlayerPosition = player.position;
        hasInitialPlayerPosition = true;
    }

    void ResetPlayerPosition()
    {
        if (player == null) return;

        if (playerSpawnPoint != null)
        {
            player.position = playerSpawnPoint.position;
            return;
        }

        if (hasInitialPlayerPosition)
            player.position = initialPlayerPosition;
    }

    // ======================
    // Helpers (Color)
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
