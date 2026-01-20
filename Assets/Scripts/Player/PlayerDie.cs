using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;
using TMPro;
using System.Linq;

/// <summary>
/// プレイヤーの死亡・ゲームオーバー・タイトル・ポーズ管理クラス
/// </summary>
public class PlayerDie : MonoBehaviour
{
    // ====== 参照 ======
    public Transform player; // プレイヤーのTransform
    public EnemySpawner spawner; // 敵スポナー

    // ====== 死亡判定 ======
    public float dieDistance = 1.0f; // プレイヤーと敵の距離がこの値以下で死亡

    // ====== UI ======
    public Image GameOverImage; // ゲームオーバー時に表示する画像
    public Image TitleImage;    // タイトル画面で表示する画像

    [Header("Pause UI")]
    public GameObject PausePanel;      // ポーズ時に表示するパネル
    public TMP_Text ResumeText;        // 「再開」テキスト
    public TMP_Text TitleBackText;     // 「タイトルへ戻る」テキスト

    public GameObject gameRoot;        // ゲーム本体のルートオブジェクト

    // ====== 入力管理 ======
    InputSystem_Actions input; // 入力アクション

    // ====== ゲーム状態管理 ======
    enum State { Title, Playing, GameOver, Stop }
    State state = State.Title; // 現在の状態

    bool transitioning = false; // 状態遷移中フラグ

    int pauseSelection = 0; // ポーズ時の選択肢（0:再開, 1:タイトルへ）

    readonly Color selectedColor = Color.red;   // 選択中のテキスト色
    readonly Color normalColor = Color.black;   // 非選択時のテキスト色

    // ====== BGM管理 ======
    [Header("BGM")]
    [Tooltip("未設定ならシーンから自動取得")]
    public BGMManager bgm; // BGM管理クラス

    // ====== SE管理 ======
    [Header("SE (One Shot)")]
    [Tooltip("未設定ならこのオブジェクトから自動取得")]
    public AudioSource seSource; // SE再生用AudioSource

    [Header("SE Clips")]
    [Tooltip("タイトル→ゲーム開始の瞬間に鳴らすSE")]
    public AudioClip startGameOneShot; // ゲーム開始時SE

    [Range(0f, 1f)]
    public float startGameOneShotVolume = 1.0f; // ゲーム開始SEの音量

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE")]
    public AudioClip hitOneShot; // 死亡時SE

    [Range(0f, 1f)]
    public float hitOneShotVolume = 1.0f; // 死亡時SEの音量

    [Tooltip("ポーズに入った瞬間に鳴らすSE")]
    public AudioClip pauseEnterOneShot; // ポーズ開始時SE

    [Range(0f, 1f)]
    public float pauseEnterOneShotVolume = 1.0f; // ポーズ開始SEの音量

    [Tooltip("ポーズ解除の瞬間に鳴らすSE")]
    public AudioClip pauseExitOneShot; // ポーズ解除時SE

    [Range(0f, 1f)]
    public float pauseExitOneShotVolume = 1.0f; // ポーズ解除SEの音量

    [Header("SE Limiter (Optional)")]
    [Tooltip("この秒数以内の連続再生は無視（連打防止）")]
    public float seMinInterval = 0.03f; // SEの連続再生防止間隔

    float lastSePlayTime = -999f; // 最後にSEを再生した時刻

    // ====== プレイヤー被ダメージ演出 ======
    [Header("Player Damage Visual")]
    [Tooltip("未設定なら player から自動取得")]
    public SpriteRenderer playerRenderer; // プレイヤーのスプライトレンダラー

    [Range(0f, 1f)]
    public float damageRedStrength = 0.75f; // ダメージ時の赤色の強さ

    public float damageHold = 0.05f; // ダメージ色を保持する時間
    public float damageFadeDuration = 0.5f; // ダメージ色からフェードアウトする時間

    // ====== スローモーション演出 ======
    [Header("Slow Motion")]
    public bool enableSlowMotion = true; // スローモーション演出を有効にするか
    public float slowMoDurationRealtime = 1.0f; // スローモーションの実時間
    [Range(0.01f, 1f)]
    public float slowMoTimeScale = 0.2f; // スローモーション時のTime.timeScale

    // ====== プレイヤー操作スクリプト ======
    [Header("Player Control Scripts")]
    [Tooltip("未設定なら player から自動取得")]
    public PlayerController playerController; // プレイヤー操作スクリプト

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    public BulletController bulletController; // 弾発射スクリプト

    [Header("Game Over Production")]
    public float gameOverShowDuration = 3.0f; // ゲームオーバー表示時間

    // ====== プレイヤーリセット ======
    [Header("Player Reset")]
    [Tooltip("タイトルに戻ったとき、プレイヤー位置をここに戻す。未設定なら Start 時の位置を使う")]
    public Transform playerSpawnPoint; // プレイヤーのリスポーン位置

    [Tooltip("StartGame 時にもリセットする（安全）")]
    public bool resetPositionOnStartGame = true; // ゲーム開始時にも位置リセットするか

    Vector3 initialPlayerPosition; // プレイヤー初期位置
    bool hasInitialPlayerPosition = false; // 初期位置キャッシュ済みか

    Color initialPlayerColor; // プレイヤー初期色
    bool hasInitialPlayerColor = false; // 初期色キャッシュ済みか

    Coroutine gameOverRoutine; // ゲームオーバー演出用コルーチン

    // ====== Unityイベント ======
    void Awake()
    {
        input = new InputSystem_Actions(); // 入力アクション初期化
    }

    void OnEnable()
    {
        input.UI.Enable(); // UI入力有効化
        input.UI.Submit.performed += OnSubmit; // 決定ボタン
        input.UI.GameStop.performed += OnGameStop; // ポーズボタン
        input.UI.UpButton.performed += OnTogglePauseSelection; // ポーズ選択肢上
        input.UI.DownButton.performed += OnTogglePauseSelection; // ポーズ選択肢下
    }

    void OnDisable()
    {
        // イベント解除
        input.UI.Submit.performed -= OnSubmit;
        input.UI.GameStop.performed -= OnGameStop;
        input.UI.UpButton.performed -= OnTogglePauseSelection;
        input.UI.DownButton.performed -= OnTogglePauseSelection;
        input.UI.Disable();

        Time.timeScale = 1f; // タイムスケールを元に戻す
        EnablePlayerControls(); // プレイヤー操作有効化
    }

    void Start()
    {
        // UI初期化
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (gameRoot != null) gameRoot.SetActive(false);

        // BGM自動取得
        if (bgm == null)
            bgm = FindFirstObjectByType<BGMManager>();

        // SE Source自動取得
        if (seSource == null)
            seSource = GetComponent<AudioSource>();
        if (seSource != null)
            seSource.playOnAwake = false;

        AutoBindPlayerRefs(); // プレイヤー関連参照自動取得

        CacheInitialPlayerColorIfNeeded(); // 初期色キャッシュ
        ResetPlayerVisual(); // プレイヤー見た目リセット
        EnablePlayerControls(); // 操作有効化

        CacheInitialPlayerPositionIfNeeded(); // 初期位置キャッシュ
        ResetPlayerPosition(); // 位置リセット

        ResetSpawnerAndEnemies(); // 敵スポナー・敵リセット

        state = State.Title; // タイトル状態に

        if (bgm != null) bgm.PlayTitle(); // タイトルBGM再生
    }

    void Update()
    {
        // プレイ中のみ死亡判定
        if (state != State.Playing) return;
        if (player == null || spawner == null) return;

        var enemies = spawner.GetSpawnedEnemies();
        float dieDistSq = dieDistance * dieDistance;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // 敵とプレイヤーの距離がdieDistance以下なら死亡
            if ((enemy.transform.position - player.position).sqrMagnitude <= dieDistSq)
            {
                OnGameOver();
                break;
            }
        }
    }

    // ======================
    // 入力イベント
    // ======================

    /// <summary>
    /// 決定ボタン入力時の処理
    /// </summary>
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

    /// <summary>
    /// ポーズボタン入力時の処理
    /// </summary>
    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        if (state == State.Playing) EnterStop();
        else if (state == State.Stop) ResumeFromStop();
    }

    /// <summary>
    /// ポーズ選択肢の上下切り替え
    /// </summary>
    void OnTogglePauseSelection(InputAction.CallbackContext ctx)
    {
        if (state != State.Stop) return;

        pauseSelection = 1 - pauseSelection; // 0⇔1を切り替え
        UpdatePauseHighlight();
    }

    // ======================
    // 状態遷移
    // ======================

    /// <summary>
    /// ゲーム開始処理
    /// </summary>
    void StartGame()
    {
        PlayStartGameOneShot(); // ゲーム開始SE

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

        ResetSpawnerAndEnemies();

        // 敵スポナー開始
        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        transitioning = false;

        if (bgm != null) bgm.PlayGame(); // ゲームBGM再生
    }

    /// <summary>
    /// ポーズ状態に遷移
    /// </summary>
    void EnterStop()
    {
        PlayPauseEnterOneShot(); // ポーズ開始SE

        state = State.Stop;
        pauseSelection = 0;
        UpdatePauseHighlight();

        if (PausePanel != null) PausePanel.SetActive(true);
        Time.timeScale = 0f; // ゲーム停止
    }

    /// <summary>
    /// ポーズ解除（ゲーム再開）
    /// </summary>
    void ResumeFromStop()
    {
        PlayPauseExitOneShot(); // ポーズ解除SE

        state = State.Playing;
        if (PausePanel != null) PausePanel.SetActive(false);
        Time.timeScale = 1f; // ゲーム再開
    }

    /// <summary>
    /// ポーズ選択肢のハイライト更新
    /// </summary>
    void UpdatePauseHighlight()
    {
        if (ResumeText != null)
            ResumeText.color = pauseSelection == 0 ? selectedColor : normalColor;

        if (TitleBackText != null)
            TitleBackText.color = pauseSelection == 1 ? selectedColor : normalColor;
    }

    // ======================
    // ゲームオーバー処理
    // ======================

    /// <summary>
    /// ゲームオーバー時の処理
    /// </summary>
    void OnGameOver()
    {
        if (state != State.Playing) return;

        transitioning = true;
        state = State.GameOver;

        if (bgm != null) bgm.StopBgmImmediate(); // BGM即停止
        PlayHitOneShot(); // 死亡SE

        DisablePlayerControls(); // 操作無効化

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
    // SE再生
    // ======================

    /// <summary>
    /// SEの連続再生防止判定
    /// </summary>
    bool CanPlaySe()
    {
        if (Time.time - lastSePlayTime < seMinInterval)
            return false;

        lastSePlayTime = Time.time;
        return true;
    }

    /// <summary>
    /// ゲーム開始SE再生
    /// </summary>
    void PlayStartGameOneShot()
    {
        if (startGameOneShot == null) return;
        if (!CanPlaySe()) return;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(startGameOneShot, transform.position, startGameOneShotVolume);
            return;
        }

        seSource.PlayOneShot(startGameOneShot, startGameOneShotVolume);
    }

    /// <summary>
    /// ポーズ開始SE再生
    /// </summary>
    void PlayPauseEnterOneShot()
    {
        if (pauseEnterOneShot == null) return;
        if (!CanPlaySe()) return;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(pauseEnterOneShot, transform.position, pauseEnterOneShotVolume);
            return;
        }

        seSource.PlayOneShot(pauseEnterOneShot, pauseEnterOneShotVolume);
    }

    /// <summary>
    /// ポーズ解除SE再生
    /// </summary>
    void PlayPauseExitOneShot()
    {
        if (pauseExitOneShot == null) return;
        if (!CanPlaySe()) return;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(pauseExitOneShot, transform.position, pauseExitOneShotVolume);
            return;
        }

        seSource.PlayOneShot(pauseExitOneShot, pauseExitOneShotVolume);
    }

    /// <summary>
    /// 死亡時SE再生
    /// </summary>
    void PlayHitOneShot()
    {
        if (hitOneShot == null) return;
        if (!CanPlaySe()) return;

        if (seSource == null)
        {
            AudioSource.PlayClipAtPoint(hitOneShot, player != null ? player.position : Vector3.zero, hitOneShotVolume);
            return;
        }

        seSource.PlayOneShot(hitOneShot, hitOneShotVolume);
    }

    /// <summary>
    /// 死亡演出の流れ（コルーチン）
    /// </summary>
    IEnumerator DeathFlow()
    {
        // 1) スローモーション
        if (enableSlowMotion)
            yield return StartCoroutine(PlaySlowMotion());

        // 2) プレイヤー赤色化＋フェード
        if (playerRenderer != null)
            yield return StartCoroutine(PlayPlayerDamageFade(playerRenderer));

        // 3) ゲームオーバーBGM
        if (bgm != null) bgm.PlayGameOver();

        // 4) ゲーム本体停止
        if (gameRoot != null) gameRoot.SetActive(false);

        // 5) GAME OVER表示
        yield return StartCoroutine(GameOverSequence());
    }

    /// <summary>
    /// スローモーション演出
    /// </summary>
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

    /// <summary>
    /// プレイヤー被ダメージ演出（赤色化→フェードアウト）
    /// </summary>
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

    /// <summary>
    /// ゲームオーバー表示・タイトル復帰演出
    /// </summary>
    IEnumerator GameOverSequence()
    {
        if (GameOverImage != null) GameOverImage.enabled = true;

        //waitforsecondsrealtimeは時間停止中でも待てる＞時間停止分待ってほしいからこれ使う
        yield return new WaitForSecondsRealtime(gameOverShowDuration);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        state = State.Title;
        transitioning = false;

        // タイトル復帰時に位置・敵リセット
        ResetPlayerPosition();
        ResetSpawnerAndEnemies();

        if (bgm != null) bgm.PlayTitle();
    }

    /// <summary>
    /// タイトル画面に戻る処理
    /// </summary>
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

        ResetSpawnerAndEnemies();

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        if (bgm != null) bgm.PlayTitle();
    }

    // ======================
    // 敵スポナー・敵リセット
    // ======================

    /// <summary>
    /// 敵スポナー停止＆全敵削除
    /// </summary>
    void ResetSpawnerAndEnemies()
    {
        if (spawner == null) return;

        spawner.StopSpawn();
        spawner.enabled = false;

        var enemies = spawner.GetSpawnedEnemies();
        if (enemies == null) return;

        // リストが変化しても安全なようにスナップショットで削除
        var snapshot = enemies.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            var e = snapshot[i];
            if (e == null) continue;
            Destroy(e);
        }
    }

    // ======================
    // プレイヤー操作ON/OFF
    // ======================

    /// <summary>
    /// プレイヤー操作・弾発射を無効化
    /// </summary>
    void DisablePlayerControls()
    {
        if (playerController != null) playerController.ControlEnabled = false;
        if (bulletController != null) bulletController.ControlEnabled = false;
    }

    /// <summary>
    /// プレイヤー操作・弾発射を有効化
    /// </summary>
    void EnablePlayerControls()
    {
        if (playerController != null) playerController.ControlEnabled = true;
        if (bulletController != null) bulletController.ControlEnabled = true;
    }

    // ======================
    // プレイヤー関連参照自動取得
    // ======================

    /// <summary>
    /// 必要な参照を自動取得
    /// </summary>
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
    // プレイヤー位置リセット
    // ======================

    /// <summary>
    /// プレイヤー初期位置をキャッシュ（未キャッシュ時のみ）
    /// </summary>
    void CacheInitialPlayerPositionIfNeeded()
    {
        if (hasInitialPlayerPosition) return;
        if (player == null) return;

        initialPlayerPosition = player.position;
        hasInitialPlayerPosition = true;
    }

    /// <summary>
    /// プレイヤー位置を初期位置またはリスポーン位置にリセット
    /// </summary>
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
    // プレイヤー色リセット
    // ======================

    /// <summary>
    /// プレイヤー初期色をキャッシュ（未キャッシュ時のみ）
    /// </summary>
    void CacheInitialPlayerColorIfNeeded()
    {
        if (hasInitialPlayerColor) return;
        if (playerRenderer == null) return;

        initialPlayerColor = playerRenderer.color;
        hasInitialPlayerColor = true;
    }

    /// <summary>
    /// プレイヤーの見た目（色）を初期状態に戻す
    /// </summary>
    void ResetPlayerVisual()
    {
        if (playerRenderer == null) return;

        Color c = hasInitialPlayerColor ? initialPlayerColor : playerRenderer.color;
        if (c.a <= 0f) c.a = 1f;
        playerRenderer.color = c;
        playerRenderer.enabled = true;
    }
}
