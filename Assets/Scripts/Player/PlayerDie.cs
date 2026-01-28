using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/*
     プレイヤーの死亡・ゲームオーバー・タイトル・ポーズを管理する

     【主な役割】
     ・ゲーム状態（Title / Playing / GameOver / Stop）を1箇所で管理する
     ・状態に応じてUI表示と入力の挙動を切り替える
     ・Playing中のみ死亡判定を行う（敵と距離で判定）
     ・GameOver演出（スローモ・被ダメ・UI表示）を順番に実行する

     【設計方針】
     ・状態遷移中の二重操作を防ぐ（IsTransitioning）
     ・transform参照はキャッシュして、同じ参照を何度も呼ばない
     ・複雑な処理は工程ごとにコメントで「何をしているか」を固定する
*/
public sealed class PlayerDie : MonoBehaviour
{
    //================
    // Reference
    //================

    [SerializeField] private Transform Player;
    [SerializeField] private EnemySpawner Spawner;


    //================
    // Death Check
    //================

    [SerializeField] private float DieDistance = 1.0f;


    //================
    // UI
    //================

    [Header("Main UI")]
    [SerializeField] private Image GameOverImage;
    [SerializeField] private Image TitleImage;

    [Header("Pause UI")]
    [SerializeField] private GameObject PausePanel;
    [SerializeField] private TMP_Text ResumeText;
    [SerializeField] private TMP_Text TitleBackText;

    [Header("Game Root")]
    [SerializeField] private GameObject GameRoot;


    //================
    // BEST / SCORE
    //================

    [Header("Best Score (Title Only)")]
    [Tooltip("未設定ならシーンから自動取得（Title状態でShow / それ以外でHide）")]
    [SerializeField] private BestScoreUI BestScoreUI;

    [Header("Score (Playing Only)")]
    [Tooltip("未設定ならシーンから自動取得（Playing状態でShowScore / それ以外でHideScore）")]
    [SerializeField] private ScoreManager ScoreManager;


    //================
    // Input
    //================

    private InputSystem_Actions Input;


    //================
    // Game State
    //================

    /*
         ゲーム状態

         Title    : タイトル表示中（ゲーム本体停止）
         Playing  : プレイ中（死亡判定あり）
         GameOver : ゲームオーバー演出中（入力制限）
         Stop     : ポーズ中（timeScale=0）
    */
    private enum State
    {
        Title,
        Playing,
        GameOver,
        Stop
    }

    private State StateCurrent = State.Title;

    /*
         状態遷移中の二重入力を防ぐ
         遷移中は true にして入力イベントを無効化する
    */
    private bool IsTransitioning = false;

    /*
         ポーズ中のメニュー選択
         0:再開
         1:タイトルへ
    */
    private int PauseSelection = 0;

    /*
         ポーズUIの色（選択中 / 通常）
    */
    private readonly Color SelectedColor = Color.red;
    private readonly Color NormalColor = Color.black;


    //================
    // BGM
    //================

    [Header("BGM")]
    [Tooltip("未設定ならシーンから自動取得")]
    [SerializeField] private BGM_Manager Bgm;


    //================
    // SE (One Shot)
    //================

    [Header("SE (One Shot)")]
    [Tooltip("未設定ならこのオブジェクトから自動取得")]
    [SerializeField] private AudioSource SeSource;

    [Header("SE Clips")]
    [SerializeField] private AudioClip StartGameOneShot;
    [Range(0f, 1f)]
    [SerializeField] private float StartGameOneShotVolume = 1.0f;

    [SerializeField] private AudioClip HitOneShot;
    [Range(0f, 1f)]
    [SerializeField] private float HitOneShotVolume = 1.0f;

    [SerializeField] private AudioClip PauseEnterOneShot;
    [Range(0f, 1f)]
    [SerializeField] private float PauseEnterOneShotVolume = 1.0f;

    [SerializeField] private AudioClip PauseExitOneShot;
    [Range(0f, 1f)]
    [SerializeField] private float PauseExitOneShotVolume = 1.0f;

    [Header("SE Limiter (Optional)")]
    [Tooltip("この秒数以内の連続再生は無視（連打防止）")]
    [SerializeField] private float SeMinInterval = 0.03f;

    private float LastSePlayTime = -999f;


    //================
    // Player Damage Visual
    //================

    [Header("Player Damage Visual")]
    [Tooltip("未設定なら Player から自動取得")]
    [SerializeField] private SpriteRenderer PlayerRenderer;

    [Range(0f, 1f)]
    [SerializeField] private float DamageRedStrength = 0.75f;

    [SerializeField] private float DamageHold = 0.05f;
    [SerializeField] private float DamageFadeDuration = 0.5f;


    //================
    // Slow Motion
    //================

    [Header("Slow Motion")]
    [SerializeField] private bool EnableSlowMotion = true;
    [SerializeField] private float SlowMoDurationRealtime = 1.0f;

    [Range(0.01f, 1f)]
    [SerializeField] private float SlowMoTimeScale = 0.2f;


    //================
    // Player Control Scripts
    //================

    [Header("Player Control Scripts")]
    [Tooltip("未設定なら Player から自動取得")]
    [SerializeField] private PlayerController PlayerController;

    [Tooltip("未設定ならシーン/Playerから自動取得（弾発射側）")]
    [SerializeField] private BulletController BulletController;


    //================
    // GameOver Production
    //================

    [Header("Game Over Production")]
    [SerializeField] private float GameOverShowDuration = 3.0f;


    //================
    // Player Reset
    //================

    [Header("Player Reset")]
    [Tooltip("タイトルに戻ったとき、プレイヤー位置をここに戻す。未設定なら Start 時の位置を使う")]
    [SerializeField] private Transform PlayerSpawnPoint;

    [Tooltip("StartGame 時にもリセットする（安全）")]
    [SerializeField] private bool ResetPositionOnStartGame = true;

    private Vector3 InitialPlayerPosition;
    private bool HasInitialPlayerPosition = false;

    private Color InitialPlayerColor;
    private bool HasInitialPlayerColor = false;

    private Coroutine GameOverRoutine;


    //================
    // Cache
    //================

    /*
         プレイヤーのTransformを保持する

         ・Update内で Player.position を何度も触らない
         ・未設定ならnullのまま（Startでエラー）
    */
    private Transform PlayerTransform;


    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             InputSystem_Actions を生成する
             入力イベントは OnEnable で登録する
        */
        Input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        /*
             UIマップを有効化
        */
        Input.UI.Enable();

        Input.UI.Submit.performed += OnSubmit;
        Input.UI.GameStop.performed += OnGameStop;
        Input.UI.UpButton.performed += OnTogglePauseSelection;
        Input.UI.DownButton.performed += OnTogglePauseSelection;
    }

    private void OnDisable()
    {
        /*
             イベント解除（解除しないと再Enable時に二重登録になる）
             停止状態が残らないように timeScale と操作も復帰する
        */
        Input.UI.Submit.performed -= OnSubmit;
        Input.UI.GameStop.performed -= OnGameStop;
        Input.UI.UpButton.performed -= OnTogglePauseSelection;
        Input.UI.DownButton.performed -= OnTogglePauseSelection;

        Input.UI.Disable();

        Time.timeScale = 1f;
        EnablePlayerControls();
    }

    private void Start()
    {
        //================
        // Initial UI
        //================

        /*
             初期状態はTitle

             ・Title表示
             ・GameRoot停止
             ・GameOver/ポーズUIは非表示
        */
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        if (PausePanel != null) PausePanel.SetActive(false);
        if (GameRoot != null) GameRoot.SetActive(false);


        //================
        // Auto Bind (Scene)
        //================

        if (Bgm == null) Bgm = FindFirstObjectByType<BGM_Manager>();
        if (Bgm == null) Debug.LogError("[PlayerDie] Bgm が未設定です（BGM_Manager をシーンに配置してください）");

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

        PlayerTransform = Player;


        //================
        // Auto Bind (Player)
        //================

        AutoBindPlayerRefs();


        //================
        // Initialize Player
        //================

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();
        EnablePlayerControls();

        CacheInitialPlayerPosition();
        ResetPlayerPosition();


        //================
        // Initialize Enemies
        //================

        ResetSpawnerAndEnemies();


        //================
        // State Apply
        //================

        StateCurrent = State.Title;
        ApplyUiVisibilityForState();

        if (Bgm != null) Bgm.PlayTitle();
    }

    private void Update()
    {
        /*
             プレイ中のみ死亡判定を行う
             Title / Stop / GameOver では判定しない
        */
        if (StateCurrent != State.Playing) return;

        if (PlayerTransform == null) return;
        if (Spawner == null) return;

        //================
        // Get Enemies
        //================

        /*
             Spawner管理下の敵を取得する
             GetSpawnedEnemies() は null を返さない前提
        */
        IReadOnlyList<GameObject> Enemies = Spawner.GetSpawnedEnemies();
        if (Enemies == null) return;

        //================
        // Distance Check
        //================

        /*
             sqrMagnitudeで距離判定する
        */
        float DieDistSq = DieDistance * DieDistance;

        Vector3 PlayerPos = PlayerTransform.position;

        for (int i = 0; i < Enemies.Count; i++)
        {
            GameObject Enemy = Enemies[i];
            if (Enemy == null) continue;

            /*
                 EnemyのTransform参照をローカルに保持する

                 ・Enemy.transform.position を何度も書かない
                 ・transformアクセス回数を減らす
            */
            Transform EnemyTransform = Enemy.transform;

            Vector3 EnemyPos = EnemyTransform.position;
            float Sq = (EnemyPos - PlayerPos).sqrMagnitude;

            if (Sq > DieDistSq) continue;

            OnGameOver();
            break;
        }
    }


    //================
    // UI Visibility
    //================

    private void ApplyUiVisibilityForState()
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
    // Input Events
    //================

    private void OnSubmit(InputAction.CallbackContext Ctx)
    {
        if (IsTransitioning) return;

        /*
             Title   : ゲーム開始
             GameOver: タイトルへ
             Stop    : 選択中のメニューを実行
        */
        if (StateCurrent == State.Title)
        {
            StartGame();
            return;
        }

        if (StateCurrent == State.GameOver)
        {
            ShowTitle();
            return;
        }

        if (StateCurrent == State.Stop)
        {
            if (PauseSelection == 0) ResumeFromStop();
            else ShowTitle();
        }
    }

    private void OnGameStop(InputAction.CallbackContext Ctx)
    {
        if (IsTransitioning) return;

        /*
             Playing: ポーズへ
             Stop   : ポーズ解除へ
        */
        if (StateCurrent == State.Playing) EnterStop();
        else if (StateCurrent == State.Stop) ResumeFromStop();
    }

    private void OnTogglePauseSelection(InputAction.CallbackContext Ctx)
    {
        /*
             Stop中のみ上下入力で選択切り替え
        */
        if (StateCurrent != State.Stop) return;

        PauseSelection = 1 - PauseSelection;
        UpdatePauseHighlight();
    }


    //================
    // State Transition
    //================

    private void StartGame()
    {
        /*
             Title → Playing に移行する

             ・UI / スコア / プレイヤー / 敵 を初期化して開始する
             ・遷移中は入力を無効化する
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

        CacheInitialPlayerPosition();
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

    private void EnterStop()
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

    private void ResumeFromStop()
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

    private void UpdatePauseHighlight()
    {
        /*
             Stop中の選択表示を更新する
        */
        if (ResumeText != null)
        {
            if (PauseSelection == 0) ResumeText.color = SelectedColor;
            if (PauseSelection != 0) ResumeText.color = NormalColor;
        }

        if (TitleBackText != null)
        {
            if (PauseSelection == 1) TitleBackText.color = SelectedColor;
            if (PauseSelection != 1) TitleBackText.color = NormalColor;
        }
    }


    //================
    // Game Over
    //================

    private void OnGameOver()
    {
        /*
             Playing中の死亡判定で呼ぶ
             GameOverへ遷移して演出を開始する
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
    // SE
    //================

    private bool CanPlaySe()
    {
        /*
             連打防止：一定時間以内の再生を無視する
        */
        if (Time.time - LastSePlayTime < SeMinInterval) return false;

        LastSePlayTime = Time.time;
        return true;
    }

    private void PlayStartGameOneShot()
    {
        if (StartGameOneShot == null) return;
        if (!CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(StartGameOneShot, StartGameOneShotVolume);
    }

    private void PlayPauseEnterOneShot()
    {
        if (PauseEnterOneShot == null) return;
        if (!CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(PauseEnterOneShot, PauseEnterOneShotVolume);
    }

    private void PlayPauseExitOneShot()
    {
        if (PauseExitOneShot == null) return;
        if (!CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(PauseExitOneShot, PauseExitOneShotVolume);
    }

    private void PlayHitOneShot()
    {
        if (HitOneShot == null) return;
        if (!CanPlaySe()) return;

        if (SeSource == null)
        {
            Debug.LogError("[PlayerDie] SeSource が未設定です（AudioSource を付けてください）");
            return;
        }

        SeSource.PlayOneShot(HitOneShot, HitOneShotVolume);
    }


    //================
    // Death Flow
    //================

    private IEnumerator DeathFlow()
    {
        /*
             死亡演出を順番に実行する

             1) スローモーション（任意）
             2) 被ダメ色 → フェード（任意）
             3) GameOverBGMへ
             4) ゲーム本体を停止
             5) GameOverUIを表示してTitleへ戻す
        */
        if (EnableSlowMotion) yield return StartCoroutine(PlaySlowMotion());
        if (PlayerRenderer != null) yield return StartCoroutine(PlayPlayerDamageFade(PlayerRenderer));

        if (Bgm != null) Bgm.PlayGameOver();

        if (GameRoot != null) GameRoot.SetActive(false);

        yield return StartCoroutine(GameOverSequence());
    }

    private IEnumerator PlaySlowMotion()
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

    private IEnumerator PlayPlayerDamageFade(SpriteRenderer Sr)
    {
        /*
             プレイヤーの色を赤くして、一定時間後に透明へフェードする

             ・赤くする強さ：DamageRedStrength
             ・赤の保持時間：DamageHold
             ・フェード時間  ：DamageFadeDuration
        */
        CacheInitialPlayerColorIfNeeded();

        Color BaseColor = Sr.color;
        if (HasInitialPlayerColor) BaseColor = InitialPlayerColor;

        Color RedColor = Color.Lerp(BaseColor, Color.red, DamageRedStrength);
        RedColor.a = BaseColor.a;

        Sr.color = RedColor;

        if (DamageHold > 0f) yield return new WaitForSecondsRealtime(DamageHold);

        float Dur = Mathf.Max(0.0001f, DamageFadeDuration);
        float t = 0f;

        while (t < Dur)
        {
            t += Time.unscaledDeltaTime;

            float a = Mathf.Clamp01(t / Dur);

            Color c = RedColor;
            c.a = Mathf.Lerp(BaseColor.a, 0f, a);

            Sr.color = c;

            yield return null;
        }

        Color End = Sr.color;
        End.a = 0f;
        Sr.color = End;
    }

    private IEnumerator GameOverSequence()
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
    // Back To Title
    //================

    private void ShowTitle()
    {
        /*
             Title状態へ戻す

             ・プレイヤー/敵/表示を初期化する
             ・timeScaleは必ず戻す
        */
        StateCurrent = State.Title;

        if (TitleImage != null) TitleImage.enabled = true;
        if (GameOverImage != null) GameOverImage.enabled = false;

        AutoBindPlayerRefs();

        CacheInitialPlayerColorIfNeeded();
        ResetPlayerVisual();

        EnablePlayerControls();

        CacheInitialPlayerPosition();
        ResetPlayerPosition();

        ResetSpawnerAndEnemies();

        if (PausePanel != null) PausePanel.SetActive(false);

        Time.timeScale = 1f;

        ApplyUiVisibilityForState();

        if (Bgm != null) Bgm.PlayTitle();
    }


    //================
    // Enemy Reset
    //================

    private void ResetSpawnerAndEnemies()
    {
        /*
             Spawnerを止めて、管理下の敵を全て破棄する

             【注意】
             ・破棄中にSpawner側のListが変わる可能性がある
             ・安全のため Snapshot（配列コピー）を作ってからDestroyする
        */
        if (Spawner == null) return;

        Spawner.StopSpawn();
        Spawner.enabled = false;

        IReadOnlyList<GameObject> Enemies = Spawner.GetSpawnedEnemies();
        if (Enemies == null) return;

        int Count = Enemies.Count;
        GameObject[] Snapshot = new GameObject[Count];

        for (int i = 0; i < Count; i++)
            Snapshot[i] = Enemies[i];

        for (int i = 0; i < Snapshot.Length; i++)
        {
            GameObject e = Snapshot[i];
            if (e == null) continue;
            Destroy(e);
        }
    }


    //================
    // Player Control ON/OFF
    //================

    private void DisablePlayerControls()
    {
        /*
             死亡中は入力と弾発射を止める
        */
        if (PlayerController != null) PlayerController.ControlEnabled = false;
        if (BulletController != null) BulletController.ControlEnabled = false;
    }

    private void EnablePlayerControls()
    {
        /*
             通常状態に戻す
        */
        if (PlayerController != null) PlayerController.ControlEnabled = true;
        if (BulletController != null) BulletController.ControlEnabled = true;
    }


    //================
    // Auto Bind Player Refs
    //================

    private void AutoBindPlayerRefs()
    {
        /*
             Player配下から参照を取得する
             非アクティブも対象にして、Title中でも拾えるようにする
        */
        if (PlayerTransform == null) PlayerTransform = Player;

        if (PlayerRenderer == null && PlayerTransform != null)
            PlayerRenderer = PlayerTransform.GetComponentInChildren<SpriteRenderer>(true);

        if (PlayerController == null && PlayerTransform != null)
            PlayerController = PlayerTransform.GetComponentInChildren<PlayerController>(true);

        if (BulletController == null && PlayerTransform != null)
            BulletController = PlayerTransform.GetComponentInChildren<BulletController>(true);
    }


    //================
    // Player Position Reset
    //================

    private void CacheInitialPlayerPosition()
    {
        /*
             初期位置を1回だけ記録する
        */
        if (HasInitialPlayerPosition) return;
        if (PlayerTransform == null) return;

        InitialPlayerPosition = PlayerTransform.position;
        HasInitialPlayerPosition = true;
    }

    private void ResetPlayerPosition()
    {
        /*
             プレイヤー位置を戻す

             ・SpawnPointがあればそこへ戻す
             ・無ければ初期位置へ戻す
        */
        if (PlayerTransform == null) return;

        if (PlayerSpawnPoint != null)
        {
            PlayerTransform.position = PlayerSpawnPoint.position;
            return;
        }

        if (HasInitialPlayerPosition)
            PlayerTransform.position = InitialPlayerPosition;
    }


    //================
    // Player Visual Reset
    //================

    private void CacheInitialPlayerColorIfNeeded()
    {
        /*
             初期色を1回だけ記録する
        */
        if (HasInitialPlayerColor) return;
        if (PlayerRenderer == null) return;

        InitialPlayerColor = PlayerRenderer.color;
        HasInitialPlayerColor = true;
    }

    private void ResetPlayerVisual()
    {
        /*
             プレイヤー表示を通常状態へ戻す

             ・alphaが0のままだと見えない事故になる
             ・念のため alpha は 1 に戻す
        */
        if (PlayerRenderer == null) return;

        Color c = PlayerRenderer.color;
        if (HasInitialPlayerColor) c = InitialPlayerColor;

        if (c.a <= 0f) c.a = 1f;

        PlayerRenderer.color = c;
        PlayerRenderer.enabled = true;
    }
}
