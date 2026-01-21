using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/*
     プレイヤーの死亡・ゲームオーバー・タイトル・ポーズを管理する
     ゲーム状態を1つにまとめて、UI表示と入力の挙動を制御する
*/
public class PlayerDie : MonoBehaviour
{
    //================
    // 参照
    //================

    public Transform Player;           // プレイヤーのTransform（座標・見た目・参照の起点）
    public EnemySpawner Spawner;       // 敵スポナー（敵一覧取得・停止に使う）

    //================
    // 死亡判定
    //================

    public float DieDistance = 1.0f;   // プレイヤーと敵の距離がこの値以下で死亡

    //================
    // UI
    //================

    public Image GameOverImage;        // ゲームオーバー時に表示する画像
    public Image TitleImage;           // タイトル画面に表示する画像

    [Header("Pause UI")]
    public GameObject PausePanel;      // ポーズ時に表示するパネル
    public TMP_Text ResumeText;        // 「再開」テキスト
    public TMP_Text TitleBackText;     // 「タイトルへ戻る」テキスト

    public GameObject GameRoot;        // ゲーム本体のルートオブジェクト（ON/OFFでまとめて止める）

    //================
    // BEST / SCORE
    //================

    [Header("Best Score (Title Only)")]
    [Tooltip("未設定ならシーンから自動取得（Title状態で Show / それ以外で Hide する）")]
    public BestScoreUI BestScoreUI;

    [Header("Score (Playing Only)")]
    [Tooltip("未設定ならシーンから自動取得（Playing状態で ShowScore / それ以外で HideScore）")]
    public ScoreManager ScoreManager;

    //================
    // 入力
    //================

    InputSystem_Actions Input;

    //================
    // ゲーム状態
    //================

    enum State { Title, Playing, GameOver, Stop }
    State StateCurrent = State.Title;

    /*
         状態遷移の途中で入力が入ると二重遷移が起きるのを防ぐ
         遷移中は true にして、入力イベントを無効にする
    */
    bool IsTransitioning = false;

    /*
         ポーズ中のメニュー選択
         0:再開
         1:タイトルへ
    */
    int PauseSelection = 0;

    // テキストの見た目（選択中/通常）
    readonly Color SelectedColor = Color.red;
    readonly Color NormalColor = Color.black;

    //================
    // BGM
    //================

    [Header("BGM")]
    [Tooltip("未設定ならシーンから自動取得")]
    public BGM_Manager Bgm;

    //================
    // SE
    //================

    [Header("SE (One Shot)")]
    [Tooltip("未設定ならこのオブジェクトから自動取得")]
    public AudioSource SeSource;

    [Header("SE Clips")]
    [Tooltip("タイトル→ゲーム開始の瞬間に鳴らすSE")]
    public AudioClip StartGameOneShot;

    [Range(0f, 1f)]
    public float StartGameOneShotVolume = 1.0f;

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE")]
    public AudioClip HitOneShot;

    [Range(0f, 1f)]
    public float HitOneShotVolume = 1.0f;

    [Tooltip("ポーズに入った瞬間に鳴らすSE")]
    public AudioClip PauseEnterOneShot;

    [Range(0f, 1f)]
    public float PauseEnterOneShotVolume = 1.0f;

    [Tooltip("ポーズ解除の瞬間のSE")]
    public AudioClip PauseExitOneShot;

    [Range(0f, 1f)]
    public float PauseExitOneShotVolume = 1.0f;

    [Header("SE Limiter (Optional)")]
    [Tooltip("この秒数以内の連続再生は無視（連打防止）")]
    public float SeMinInterval = 0.03f;

    float LastSePlayTime = -999f;

    //================
    // プレイヤー被ダメージ演出
    //================

    [Header("Player Damage Visual")]
    [Tooltip("未設定なら Player から自動取得")]
    public SpriteRenderer PlayerRenderer;

    [Range(0f, 1f)]
    public float DamageRedStrength = 0.75f;

    public float DamageHold = 0.05f;
    public float DamageFadeDuration = 0.5f;

    //================
    // スローモーション演出
    //================

    [Header("Slow Motion")]
    public bool EnableSlowMotion = true;
    public float SlowMoDurationRealtime = 1.0f;

    [Range(0.01f, 1f)]
    public float SlowMoTimeScale = 0.2f;

    //================
    // プレイヤー操作スクリプト
    //================

    [Header("Player Control Scripts")]
    [Tooltip("未設定なら Player から自動取得")]
    public PlayerController PlayerController;

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    public BulletController BulletController;

    //================
    // GameOver演出
    //================

    [Header("Game Over Production")]
    public float GameOverShowDuration = 3.0f;

    //================
    // プレイヤーリセット
    //================

    [Header("Player Reset")]
    [Tooltip("タイトルに戻ったとき、プレイヤー位置をここに戻す。未設定なら Start 時の位置を使う")]
    public Transform PlayerSpawnPoint;

    [Tooltip("StartGame 時にもリセットする（安全）")]
    public bool ResetPositionOnStartGame = true;

    Vector3 InitialPlayerPosition;
    bool HasInitialPlayerPosition = false;

    Color InitialPlayerColor;
    bool HasInitialPlayerColor = false;

    Coroutine GameOverRoutine;

    //================
    // Unity Event
    //================

    void Awake()
    {
        /*
             InputSystem_Actions を生成する
             入力イベントは OnEnable で登録する
        */
        Input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        /*
             UIマップを有効化（操作対象をUIに固定）
             ボタンが押されたらイベントで処理する
        */
        Input.UI.Enable();

        Input.UI.Submit.performed += OnSubmit;
        Input.UI.GameStop.performed += OnGameStop;
        Input.UI.UpButton.performed += OnTogglePauseSelection;
        Input.UI.DownButton.performed += OnTogglePauseSelection;
    }

    void OnDisable()
    {
        /*
             イベント解除（解除しないと再Enable時に二重登録になる）
             停止状態が残らないように、timeScale と操作を復帰する
        */
        Input.UI.Submit.performed -= OnSubmit;
        Input.UI.GameStop.performed -= OnGameStop;
        Input.UI.UpButton.performed -= OnTogglePauseSelection;
        Input.UI.DownButton.performed -= OnTogglePauseSelection;

        Input.UI.Disable();

        Time.timeScale = 1f;
        EnablePlayerControls();
    }

    void Start()
    {
        /*
             初期状態を Title にする
             Title表示 / GameRoot停止 / ポーズUI非表示
        */
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (PausePanel != null) PausePanel.SetActive(false);
        if (GameRoot != null) GameRoot.SetActive(false);

        //================
        // 参照の自動取得
        //================

        if (Bgm == null) Bgm = FindFirstObjectByType<BGM_Manager>();
        if (Bgm == null) Debug.LogError("[PlayerDie] Bgm が未設定です（BGMManager をシーンに配置してください）");

        if (SeSource == null) SeSource = GetComponent<AudioSource>();
        if (SeSource == null) Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
        if (SeSource != null) SeSource.playOnAwake = false;

        if (ScoreManager == null) ScoreManager = FindFirstObjectByType<ScoreManager>();
        if (ScoreManager == null) Debug.LogError("[PlayerDie] ScoreManager が未設定です（ScoreManager をシーンに配置してください）");

        if (BestScoreUI == null) BestScoreUI = FindFirstObjectByType<BestScoreUI>();
        if (BestScoreUI == null) Debug.LogError("[PlayerDie] BestScoreUI が未設定です（BestScoreUI をシーンに配置してください）");

        if (Player == null) Debug.LogError("[PlayerDie] Player が未設定です（Transform を設定してください）");
        if (Spawner == null) Debug.LogError("[PlayerDie] Spawner が未設定です（EnemySpawner を設定してください）");
        if (GameRoot == null) Debug.LogError("[PlayerDie] GameRoot が未設定です（ゲーム本体のルートを設定してください）");

        //================
        // Player参照の自動取得
        //================

        AutoBindPlayerRefs();

        //================
        // 初期化
        //================

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

        CacheInitialPlayerPositionIfNeeded();
        ResetPlayerPosition();

        ResetSpawnerAndEnemies();

        StateCurrent = State.Title;

        ApplyUiVisibilityForState();

        if (Bgm != null) Bgm.PlayTitle();
    }

    void Update()
    {
        /*
             プレイ中のみ死亡判定
             Title / Stop / GameOver では判定しない
        */
        if (StateCurrent != State.Playing) return;
        if (Player == null || Spawner == null) return;

        /*
             Spawner 管理下の敵を取得する
             GetSpawnedEnemies() は null を返さない前提
        */
        IReadOnlyList<GameObject> Enemies = Spawner.GetSpawnedEnemies();

        /*
             sqrMagnitude で距離判定する
             sqrt を避けて軽量化する
        */
        float DieDistSq = DieDistance * DieDistance;

        for (int i = 0; i < Enemies.Count; i++)
        {
            GameObject Enemy = Enemies[i];
            if (Enemy == null) continue;

            if ((Enemy.transform.position - Player.position).sqrMagnitude > DieDistSq) continue;

            OnGameOver();
            break;
        }
    }

    //================
    // UI表示制御
    //================

    void ApplyUiVisibilityForState()
    {
        /*
             BEST：Titleだけ表示
        */
        if (BestScoreUI != null)
        {
            if (StateCurrent == State.Title) BestScoreUI.Show();
            else BestScoreUI.Hide();
        }

        /*
             SCORE：Playingだけ表示
        */
        if (ScoreManager != null)
        {
            if (StateCurrent == State.Playing) ScoreManager.ShowScore();
            else ScoreManager.HideScore();
        }
    }

    //================
    // 入力イベント
    //================

    void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (IsTransitioning) return;

        /*
             Title：ゲーム開始
             GameOver：タイトルへ
             Stop：選択中のメニューを実行
        */
        if (StateCurrent == State.Title) { StartGame(); return; }
        if (StateCurrent == State.GameOver) { ShowTitle(); return; }

        if (StateCurrent == State.Stop)
        {
            if (PauseSelection == 0) ResumeFromStop();
            else ShowTitle();
        }
    }

    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (IsTransitioning) return;

        /*
             Playing：ポーズへ
             Stop：ポーズ解除へ
        */
        if (StateCurrent == State.Playing) EnterStop();
        else if (StateCurrent == State.Stop) ResumeFromStop();
    }

    void OnTogglePauseSelection(InputAction.CallbackContext ctx)
    {
        /*
             Stop中のみ上下入力で選択切り替え
        */
        if (StateCurrent != State.Stop) return;

        PauseSelection = 1 - PauseSelection;
        UpdatePauseHighlight();
    }

    //================
    // 状態遷移
    //================

    void StartGame()
    {
        /*
             Title → Playing に移行する
             UI / スコア / プレイヤー / 敵 を初期化して開始する
        */
        PlayStartGameOneShot();

        IsTransitioning = true;
        StateCurrent = State.Playing;

        if (TitleImage != null) TitleImage.enabled = false;
        if (GameOverImage != null) GameOverImage.enabled = false;

        if (GameRoot != null) GameRoot.SetActive(true);

        if (ScoreManager != null) ScoreManager.ResetScore();

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

        CacheInitialPlayerPositionIfNeeded();
        if (ResetPositionOnStartGame) ResetPlayerPosition();

        ResetSpawnerAndEnemies();

        /*
             敵スポナー開始
        */
        if (Spawner != null)
        {
            Spawner.enabled = true;
            Spawner.StartSpawn();
        }

        ApplyUiVisibilityForState();

        IsTransitioning = false;

        if (Bgm != null) Bgm.PlayGame();
    }

    void EnterStop()
    {
        /*
             Playing → Stop に移行する
             timeScale を 0 にして停止する
        */
        PlayPauseEnterOneShot();

        StateCurrent = State.Stop;
        PauseSelection = 0;

        UpdatePauseHighlight();

        if (PausePanel != null) PausePanel.SetActive(true);

        Time.timeScale = 0f;
    }

    void ResumeFromStop()
    {
        /*
             Stop → Playing に戻す
             timeScale を 1 に戻して再開する
        */
        PlayPauseExitOneShot();

        StateCurrent = State.Playing;

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        ApplyUiVisibilityForState();
    }

    void UpdatePauseHighlight()
    {
        /*
             Stop中の選択表示を更新する
        */
        if (ResumeText != null)
            ResumeText.color = PauseSelection == 0 ? SelectedColor : NormalColor;

        if (TitleBackText != null)
            TitleBackText.color = PauseSelection == 1 ? SelectedColor : NormalColor;
    }

    //================
    // ゲームオーバー処理
    //================

    void OnGameOver()
    {
        /*
             Playing 中の死亡判定で呼ぶ
             GameOver へ遷移して演出を開始する
        */
        if (StateCurrent != State.Playing) return;

        IsTransitioning = true;
        StateCurrent = State.GameOver;

        if (ScoreManager != null) ScoreManager.CommitScoreToBestTop3();

        if (Bgm != null) Bgm.StopBgmImmediate();
        PlayHitOneShot();

        DisablePlayerControls();

        if (PausePanel != null) PausePanel.SetActive(false);

        if (Spawner != null)
        {
            Spawner.StopSpawn();
            Spawner.enabled = false;
        }

        ApplyUiVisibilityForState();

        if (GameOverRoutine != null) StopCoroutine(GameOverRoutine);
        GameOverRoutine = StartCoroutine(DeathFlow());
    }

    //================
    // SE再生
    //================

    bool CanPlaySe()
    {
        /*
             連打防止：一定時間以内の再生を無視する
        */
        if (Time.time - LastSePlayTime < SeMinInterval) return false;

        LastSePlayTime = Time.time;
        return true;
    }

    void PlayStartGameOneShot()
    {
        /*
             タイトル → ゲーム開始のSE
        */
        if (StartGameOneShot == null || !CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(StartGameOneShot, StartGameOneShotVolume);
    }

    void PlayPauseEnterOneShot()
    {
        /*
             ポーズに入った瞬間のSE
        */
        if (PauseEnterOneShot == null || !CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(PauseEnterOneShot, PauseEnterOneShotVolume);
    }

    void PlayPauseExitOneShot()
    {
        /*
             ポーズ解除の瞬間のSE
        */
        if (PauseExitOneShot == null || !CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(PauseExitOneShot, PauseExitOneShotVolume);
    }

    void PlayHitOneShot()
    {
        /*
             被弾（死亡）した瞬間のSE
        */
        if (HitOneShot == null || !CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(HitOneShot, HitOneShotVolume);
    }

    //================
    // 死亡演出（コルーチン）
    //================

    IEnumerator DeathFlow()
    {
        /*
             死亡演出を順番に実行する
             スローモーション → 被ダメ色 → BGM切替 → GameOver表示
        */
        if (EnableSlowMotion) yield return StartCoroutine(PlaySlowMotion());
        if (PlayerRenderer != null) yield return StartCoroutine(PlayPlayerDamageFade(PlayerRenderer));

        if (Bgm != null) Bgm.PlayGameOver();

        if (GameRoot != null) GameRoot.SetActive(false);

        yield return StartCoroutine(GameOverSequence());
    }

    IEnumerator PlaySlowMotion()
    {
        /*
             一定時間だけ timeScale を下げる
             WaitForSecondsRealtime で実時間待機する
        */
        float PrevScale = Time.timeScale;
        float PrevFixed = Time.fixedDeltaTime;

        float s = Mathf.Clamp(SlowMoTimeScale, 0.01f, 1f);
        Time.timeScale = s;
        Time.fixedDeltaTime = PrevFixed * s;

        float Wait = Mathf.Max(0f, SlowMoDurationRealtime);
        if (Wait > 0f) yield return new WaitForSecondsRealtime(Wait);

        Time.timeScale = PrevScale;
        Time.fixedDeltaTime = PrevFixed;
    }

    IEnumerator PlayPlayerDamageFade(SpriteRenderer sr)
    {
        /*
             プレイヤーの色を赤くして、一定時間後に透明へフェードする
        */
        CacheInitialPlayerColorIfNeeded();

        Color BaseColor = HasInitialPlayerColor ? InitialPlayerColor : sr.color;

        Color RedColor = Color.Lerp(BaseColor, Color.red, DamageRedStrength);
        RedColor.a = BaseColor.a;

        sr.color = RedColor;

        if (DamageHold > 0f) yield return new WaitForSecondsRealtime(DamageHold);

        float Dur = Mathf.Max(0.0001f, DamageFadeDuration);
        float t = 0f;

        while (t < Dur)
        {
            t += Time.unscaledDeltaTime;

            float a = Mathf.Clamp01(t / Dur);

            Color c = RedColor;
            c.a = Mathf.Lerp(BaseColor.a, 0f, a);

            sr.color = c;

            yield return null;
        }

        Color End = sr.color;
        End.a = 0f;
        sr.color = End;
    }

    IEnumerator GameOverSequence()
    {
        /*
             GameOver UI を表示して一定時間待つ
             終了後に Title へ戻す
        */
        if (GameOverImage != null) GameOverImage.enabled = true;

        yield return new WaitForSecondsRealtime(GameOverShowDuration);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        StateCurrent = State.Title;
        IsTransitioning = false;

        ResetPlayerPosition();
        ResetSpawnerAndEnemies();

        ApplyUiVisibilityForState();

        if (Bgm != null) Bgm.PlayTitle();
    }

    //================
    // Titleへ戻す
    //================

    void ShowTitle()
    {
        /*
             Title 状態へ戻す
             プレイヤー/敵/表示を初期化する
        */
        StateCurrent = State.Title;

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

        ApplyUiVisibilityForState();

        if (Bgm != null) Bgm.PlayTitle();
    }

    //================
    // 敵リセット
    //================

    void ResetSpawnerAndEnemies()
    {
        /*
             Spawnerを止めて、管理下の敵を全て破棄する
             破棄中にリストが変わる事故を避けるため、Snapshotを作る
        */
        if (Spawner == null) return;

        Spawner.StopSpawn();
        Spawner.enabled = false;

        /*
             GetSpawnedEnemies() は null を返さない前提
        */
        IReadOnlyList<GameObject> Enemies = Spawner.GetSpawnedEnemies();

        GameObject[] Snapshot = Enemies.ToArray();
        for (int i = 0; i < Snapshot.Length; i++)
        {
            GameObject e = Snapshot[i];
            if (e == null) continue;
            Destroy(e);
        }
    }

    //================
    // プレイヤー操作ON/OFF
    //================

    void DisablePlayerControls()
    {
        /*
             死亡中は入力と弾発射を止める
        */
        if (PlayerController != null) PlayerController.ControlEnabled = false;
        if (BulletController != null) BulletController.ControlEnabled = false;
    }

    void EnablePlayerControls()
    {
        /*
             通常状態に戻す
        */
        if (PlayerController != null) PlayerController.ControlEnabled = true;
        if (BulletController != null) BulletController.ControlEnabled = true;
    }

    //================
    // Player参照の自動取得
    //================

    void AutoBindPlayerRefs()
    {
        /*
             Player配下から参照を取得する
             非アクティブも対象にして、Title中でも拾えるようにする
        */
        if (PlayerRenderer == null && Player != null)
            PlayerRenderer = Player.GetComponentInChildren<SpriteRenderer>(true);

        if (PlayerController == null && Player != null)
            PlayerController = Player.GetComponentInChildren<PlayerController>(true);

        if (BulletController == null && Player != null)
            BulletController = Player.GetComponentInChildren<BulletController>(true);
    }

    //================
    // プレイヤー位置リセット
    //================

    void CacheInitialPlayerPositionIfNeeded()
    {
        /*
             初期位置を1回だけ記録する
        */
        if (HasInitialPlayerPosition || Player == null) return;

        InitialPlayerPosition = Player.position;
        HasInitialPlayerPosition = true;
    }

    void ResetPlayerPosition()
    {
        /*
             プレイヤー位置を戻す
             SpawnPointがあればそこへ、なければ初期位置へ戻す
        */
        if (Player == null) return;

        if (PlayerSpawnPoint != null)
        {
            Player.position = PlayerSpawnPoint.position;
            return;
        }

        if (HasInitialPlayerPosition) Player.position = InitialPlayerPosition;
    }

    //================
    // プレイヤー色リセット
    //================

    void CacheInitialPlayerColorIfNeeded()
    {
        /*
             初期色を1回だけ記録する
        */
        if (HasInitialPlayerColor || PlayerRenderer == null) return;

        InitialPlayerColor = PlayerRenderer.color;
        HasInitialPlayerColor = true;
    }

    void ResetPlayerVisual()
    {
        /*
             プレイヤー表示を通常状態へ戻す
             alphaが0のままだと見えない事故になるので保険を入れる
        */
        if (PlayerRenderer == null) return;

        Color c = HasInitialPlayerColor ? InitialPlayerColor : PlayerRenderer.color;
        if (c.a <= 0f) c.a = 1f;

        PlayerRenderer.color = c;
        PlayerRenderer.enabled = true;
    }
}
