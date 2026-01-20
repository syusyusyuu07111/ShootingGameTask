using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Linq;

/// <summary>
/// プレイヤーの死亡・ゲームオーバー・タイトル・ポーズ管理クラス
///
/// ・enum State + state変数 で「ゲーム状態」を1つにまとめる
/// </summary>
public class PlayerDie : MonoBehaviour
{
    // ====== 参照 ======
    public Transform player;           // プレイヤーのTransform（座標・見た目・参照の起点）
    public EnemySpawner spawner;       // 敵スポナー（敵一覧取得・停止に使う）

    // ====== 死亡判定 ======
    public float dieDistance = 1.0f;   // プレイヤーと敵の距離がこの値以下で死亡

    // ====== UI ======
    public Image GameOverImage;        // ゲームオーバー時に表示する画像
    public Image TitleImage;           // タイトル画面で表示する画像

    [Header("Pause UI")]
    public GameObject PausePanel;      // ポーズ時に表示するパネル
    public TMP_Text ResumeText;        // 「再開」テキスト
    public TMP_Text TitleBackText;     // 「タイトルへ戻る」テキスト

    public GameObject gameRoot;        // ゲーム本体のルートオブジェクト（ON/OFFでまとめて止める）

    // ====== 入力管理 ======

    InputSystem_Actions input;

    // ====== ゲーム状態管理 ======
    // 【enum State】
    // ・ゲームの状態（Title/Playing/GameOver/Stop）
    enum State { Title, Playing, GameOver, Stop }
    State state = State.Title;

    // transitioning：
    // ・状態遷移の途中で入力が入ると二重遷移が起きるのを防ぐフラグ
    bool transitioning = false;

    // ポーズ中のメニュー選択（0:再開, 1:タイトルへ）
    // 【小技】1 - pauseSelection で 0<->1 を切り替えられる
    int pauseSelection = 0;

    // テキストの見た目（選択中/通常）
    readonly Color selectedColor = Color.red;
    readonly Color normalColor = Color.black;

    // ====== BGM管理 ======
    [Header("BGM")]
    [Tooltip("未設定ならシーンから自動取得")]
    public BGMManager bgm;

    // ====== SE管理 ======
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

    [Tooltip("ポーズに入った瞬間に鳴らすSE")]
    public AudioClip pauseEnterOneShot;

    [Range(0f, 1f)]
    public float pauseEnterOneShotVolume = 1.0f;

    [Tooltip("ポーズ解除の瞬間に鳴らすSE")]
    public AudioClip pauseExitOneShot;

    [Range(0f, 1f)]
    public float pauseExitOneShotVolume = 1.0f;

    [Header("SE Limiter (Optional)")]
    [Tooltip("この秒数以内の連続再生は無視（連打防止）")]
    public float seMinInterval = 0.03f;

    // lastSePlayTime：
    // ・Time.timeで管理
    // ・timeScale=0の時はTime.timeも進まない
    float lastSePlayTime = -999f;

    // ====== プレイヤー被ダメージ演出 ======
    [Header("Player Damage Visual")]
    [Tooltip("未設定なら player から自動取得")]
    public SpriteRenderer playerRenderer;

    [Range(0f, 1f)]
    public float damageRedStrength = 0.75f;

    public float damageHold = 0.05f;
    public float damageFadeDuration = 0.5f;

    // ====== スローモーション演出 ======
    [Header("Slow Motion")]
    public bool enableSlowMotion = true;
    public float slowMoDurationRealtime = 1.0f;

    [Range(0.01f, 1f)]
    public float slowMoTimeScale = 0.2f;

    // ====== プレイヤー操作スクリプト ======
    [Header("Player Control Scripts")]
    [Tooltip("未設定なら player から自動取得")]
    public PlayerController playerController;

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    public BulletController bulletController;

    [Header("Game Over Production")]
    public float gameOverShowDuration = 3.0f;

    // ====== プレイヤーリセット ======
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
        // InputSystemの生成
        input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        // UIマップを有効化（操作対象をUIに固定）
        input.UI.Enable();

        // performed +=
        // ボタンが押されたら呼ばれる）
        // オブジェクトが有効な間だけ入力を受ける
        input.UI.Submit.performed += OnSubmit;
        input.UI.GameStop.performed += OnGameStop;
        input.UI.UpButton.performed += OnTogglePauseSelection;
        input.UI.DownButton.performed += OnTogglePauseSelection;
    }

    void OnDisable()
    {
        // イベント解除
        // ・解除しないと、再Enable時に二重登録になり 1回押したのに2回反応などが起きる
        input.UI.Submit.performed -= OnSubmit;
        input.UI.GameStop.performed -= OnGameStop;
        input.UI.UpButton.performed -= OnTogglePauseSelection;
        input.UI.DownButton.performed -= OnTogglePauseSelection;

        input.UI.Disable();

        // 念のため復帰
        Time.timeScale = 1f;
        EnablePlayerControls();
    }

    void Start()
    {
        // UI初期化タイトル→ゲームの表示切替
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (gameRoot != null) gameRoot.SetActive(false);

        // BGM取得
        if (bgm == null)
            bgm = FindFirstObjectByType<BGMManager>();

        // SE Source取得
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

        ResetSpawnerAndEnemies();

        state = State.Title;

        if (bgm != null) bgm.PlayTitle();
    }

    void Update()
    {
        // プレイ中のみ死亡判定（Title/Stop/GameOverでは判定しない）
        if (state != State.Playing) return;
        if (player == null || spawner == null) return;

        var enemies = spawner.GetSpawnedEnemies();

        // 【sqrMagnitudeで距離判定】
        // ・Vector3.Distance は内部で sqrt が走る
        // ・比較だけなら平方で比較すると軽い
        float dieDistSq = dieDistance * dieDistance;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;

            // (敵座標 - プレイヤー座標) = 方向ベクトル
            // sqrMagnitude = 長さの二乗
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

    void OnSubmit(InputAction.CallbackContext ctx)
    {
        // transitioning中は入力無視（状態遷移の事故防止）
        if (transitioning) return;

        // 状態ごとに「決定」ボタンの意味が変わる（状態機械の典型）
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
            // pauseSelectionの意味：0=再開 / 1=タイトルへ
            if (pauseSelection == 0) ResumeFromStop();
            else ShowTitle();
        }
    }

    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        // Playing中はStopへ、Stop中はPlayingへ戻す（トグル）
        if (state == State.Playing) EnterStop();
        else if (state == State.Stop) ResumeFromStop();
    }

    void OnTogglePauseSelection(InputAction.CallbackContext ctx)
    {
        // ポーズ画面以外では上下を無視
        if (state != State.Stop) return;

        // 0⇔1切り替え（2択を軽く書けるテク）
        pauseSelection = 1 - pauseSelection;
        UpdatePauseHighlight();
    }

    // ======================
    // 状態遷移
    // ======================

    void StartGame()
    {
        // タイトル→ゲーム開始の“瞬間”に鳴らす
        PlayStartGameOneShot();

        transitioning = true;   // この処理中は入力を無視したい
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

        if (bgm != null) bgm.PlayGame();
    }

    void EnterStop()
    {
        PlayPauseEnterOneShot();

        state = State.Stop;
        pauseSelection = 0;
        UpdatePauseHighlight();

        if (PausePanel != null) PausePanel.SetActive(true);


        Time.timeScale = 0f;
    }

    void ResumeFromStop()
    {
        PlayPauseExitOneShot();

        state = State.Playing;
        if (PausePanel != null) PausePanel.SetActive(false);

        // 止めたゲーム時間を戻す
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
    // ゲームオーバー処理
    // ======================

    void OnGameOver()
    {
        if (state != State.Playing) return;

        transitioning = true;
        state = State.GameOver;

        // 即切り替えたいのでStopBgmImmediate
        if (bgm != null) bgm.StopBgmImmediate();
        PlayHitOneShot();

        // 死亡演出中に動けないように操作停止
        DisablePlayerControls();

        if (PausePanel != null) PausePanel.SetActive(false);

        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        // DeathFlowの二重起動防止
        if (gameOverRoutine != null) StopCoroutine(gameOverRoutine);
        gameOverRoutine = StartCoroutine(DeathFlow());
    }

    // ======================
    // SE再生
    // ======================

    bool CanPlaySe()
    {
        // 連打防止（短時間で多重再生すると耳が死ぬ＆音割れしやすい）
        if (Time.time - lastSePlayTime < seMinInterval)
            return false;

        lastSePlayTime = Time.time;
        return true;
    }

    void PlayStartGameOneShot()
    {
        if (startGameOneShot == null) return;
        if (!CanPlaySe()) return;

        if (seSource == null)
        {
            // その場で鳴らす　SESOURCEなくてもなる
            AudioSource.PlayClipAtPoint(startGameOneShot, transform.position, startGameOneShotVolume);
            return;
        }

        // clipを汚さず単発で鳴らせる（SE向き）
        seSource.PlayOneShot(startGameOneShot, startGameOneShotVolume);
    }

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

    // ======================
    // 死亡演出（コルーチンで順番に実行）
    // ======================

    IEnumerator DeathFlow()
    {
        // Coroutineを “流れ” として書くと
        // 「この演出→次にこれ→最後にこれ」がコード順にそのまま読める

        // 1) スローモーション（timeScaleをいじる）
        if (enableSlowMotion)
            yield return StartCoroutine(PlaySlowMotion());

        // 2) プレイヤー赤色化＋フェード（見た目演出）
        if (playerRenderer != null)
            yield return StartCoroutine(PlayPlayerDamageFade(playerRenderer));

        // 3) ゲームオーバーBGM
        if (bgm != null) bgm.PlayGameOver();

        // 4) ゲーム本体停止（UIは残してゲームだけ止めたい）
        if (gameRoot != null) gameRoot.SetActive(false);

        // 5) GAME OVER表示→一定時間→タイトル復帰
        yield return StartCoroutine(GameOverSequence());
    }

    IEnumerator PlaySlowMotion()
    {
        // スローモーションは Time.timeScale を下げる

        float prevScale = Time.timeScale;
        float prevFixed = Time.fixedDeltaTime;

        float s = Mathf.Clamp(slowMoTimeScale, 0.01f, 1f);
        Time.timeScale = s;
        Time.fixedDeltaTime = prevFixed * s;

        // WaitForSecondsRealtime：
        // timeScaleの影響を受けない
        float wait = Mathf.Max(0f, slowMoDurationRealtime);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        // 元に戻す
        Time.timeScale = prevScale;
        Time.fixedDeltaTime = prevFixed;
    }

    IEnumerator PlayPlayerDamageFade(SpriteRenderer sr)
    {
        CacheInitialPlayerColorIfNeeded();

        Color baseColor = hasInitialPlayerColor ? initialPlayerColor : sr.color;

        // Color.Lerp：色Lerpできる
        Color redColor = Color.Lerp(baseColor, Color.red, damageRedStrength);
        redColor.a = baseColor.a;
        sr.color = redColor;

        // WaitForSecondsRealtime：timeScale=0でも待てる（演出を止めたくないからこれつかう）
        if (damageHold > 0f)
            yield return new WaitForSecondsRealtime(damageHold);

        float dur = Mathf.Max(0.0001f, damageFadeDuration);
        float t = 0f;

        // unscaledDeltaTime：
        // timeScaleに影響されない経過秒
        // ポーズやスロー中でもフェード演出が進む
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);

            // ここでは「透明に向かってフェード」
            Color c = redColor;
            c.a = Mathf.Lerp(baseColor.a, 0f, a);
            sr.color = c;

            yield return null;
        }

        // 最終的に完全透明へ（誤差消し）
        Color end = sr.color;
        end.a = 0f;
        sr.color = end;
    }

    IEnumerator GameOverSequence()
    {
        if (GameOverImage != null) GameOverImage.enabled = true;

        // WaitForSecondsRealtime：
        // ・Time.timeScale=0でも進む待機
        yield return new WaitForSecondsRealtime(gameOverShowDuration);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        state = State.Title;
        transitioning = false;

        // タイトル復帰時に位置・敵リセット（次のプレイに備える）
        ResetPlayerPosition();
        ResetSpawnerAndEnemies();

        if (bgm != null) bgm.PlayTitle();
    }

    void ShowTitle()
    {
        state = State.Title;

        if (TitleImage != null) TitleImage.enabled = true;
        if (GameOverImage != null) GameOverImage.enabled = false;

        // =====================================================
        // 「ゲーム開始/タイトル復帰」の前に、状態を初期化する
        // =====================================================

        // 1) 参照の取得
        // Titleでは gameRoot がOFFになってたりして参照が切れることがあるので、毎回取り直す
        AutoBindPlayerRefs();

        // 2) 見た目の初期化（色）
        // 被ダメージ演出で playerRenderer.color の alpha を0にして透明になったまま戻る事故を防ぐ
        // 初期色をまだ保存していないならキャッシュして、そこへ戻す
        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();

        // 3) 操作の再有効化
        // GameOver時に操作を止めているので、タイトル復帰/開始時に必ず戻す
        // 入力だけを止める設計なので、スクリプト自体を無効化しなくても再開できる
        EnablePlayerControls();

        // 4) 位置の初期化
        // 初期位置をまだ保存していないならキャッシュして、そこへ戻す
        // playerSpawnPoint があるならそこへ、無ければStart時の位置へ戻す
        CacheInitialPlayerPositionIfNeeded();
        ResetPlayerPosition();

        // 5) 敵の初期化
        // 前回のプレイの敵が残っていると、開始直後に即死したりするので必ず全削除
        // Spawnerも止めて敵が湧かない状態に戻す
        ResetSpawnerAndEnemies();


        if (PausePanel != null) PausePanel.SetActive(false);

        // 念のため戻す（ポーズ状態が残る事故防止）
        Time.timeScale = 1f;

        if (bgm != null) bgm.PlayTitle();
    }

    // ======================
    // 敵スポナー・敵リセット
    // ======================

    void ResetSpawnerAndEnemies()
    {
        if (spawner == null) return;

        spawner.StopSpawn();
        spawner.enabled = false;

        var enemies = spawner.GetSpawnedEnemies();
        if (enemies == null) return;

        // 【ToArray()で消す敵をけす】
        // ・元のIReadOnlyListが裏で更新されても、今の一覧を安全に消せる
        // ・foreach中にDestroy/Removeが走るとコレクション変更例外になるのを避ける
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

    void DisablePlayerControls()
    {
        // 操作スクリプト側にフラグを渡して停止する設計
        // ・ControllerのUpdateは回っていても、入力処理を止められるための設計
        if (playerController != null) playerController.ControlEnabled = false;
        if (bulletController != null) bulletController.ControlEnabled = false;
    }

    void EnablePlayerControls()
    {
        if (playerController != null) playerController.ControlEnabled = true;
        if (bulletController != null) bulletController.ControlEnabled = true;
    }

    // ======================
    // プレイヤー関連参照自動取得
    // ======================

    void AutoBindPlayerRefs()
    {
        // GetComponentInChildren：
        // ・子階層から探す
        // ・trueで非アクティブも対象（Title時にgameRootがOFFでも拾えるようにする）
        if (playerRenderer == null && player != null)
            playerRenderer = player.GetComponentInChildren<SpriteRenderer>(true);

        if (playerController == null && player != null)
            playerController = player.GetComponentInChildren<PlayerController>(true);

        if (bulletController == null)
        {
            if (player != null)
                bulletController = player.GetComponentInChildren<BulletController>(true);

            // プレイヤー側で取れない場合、シーン全体から探す最終手段
            if (bulletController == null)
                bulletController = FindFirstObjectByType<BulletController>();
        }
    }

    // ======================
    // プレイヤー位置リセット
    // ======================

    void CacheInitialPlayerPositionIfNeeded()
    {
        // 初期位置は一度だけキャッシュ（Start時の位置を基準にしたい）
        if (hasInitialPlayerPosition) return;
        if (player == null) return;

        initialPlayerPosition = player.position;
        hasInitialPlayerPosition = true;
    }

    void ResetPlayerPosition()
    {
        if (player == null) return;

        // SpawnPointが指定されていればそれを優先
        if (playerSpawnPoint != null)
        {
            player.position = playerSpawnPoint.position;
            return;
        }

        // 無ければ最初にキャッシュした位置へ
        if (hasInitialPlayerPosition)
            player.position = initialPlayerPosition;
    }

    // ======================
    // プレイヤー色リセット
    // ======================

    void CacheInitialPlayerColorIfNeeded()
    {
        // 初期色も一度だけキャッシュ（ダメージ演出で色を変えるので戻す必要がある）
        if (hasInitialPlayerColor) return;
        if (playerRenderer == null) return;

        initialPlayerColor = playerRenderer.color;
        hasInitialPlayerColor = true;
    }

    void ResetPlayerVisual()
    {
        if (playerRenderer == null) return;

        // 透明になっていたら戻す（ダメージ演出でalphaを落としているため）
        Color c = hasInitialPlayerColor ? initialPlayerColor : playerRenderer.color;
        if (c.a <= 0f) c.a = 1f;

        playerRenderer.color = c;
        playerRenderer.enabled = true;
    }
}
