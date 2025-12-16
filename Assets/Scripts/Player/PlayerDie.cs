using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

public class PlayerDie : MonoBehaviour
{
    public Transform player;
    public EnemySpawner spawner;

    public float dieDistance = 1.0f;

    public Image GameOverImage;
    public Image TitleImage;
    public Image GameStopImage;     // 一時停止画面

    public GameObject gameRoot;     // ★ゲーム中の親（UI/このPlayerDie自身は入れない）

    InputSystem_Actions input;

    enum State { Title, Playing, GameOver, Stop }
    State state = State.Title;

    bool transitioning = false;

    void Awake()
    {
        input = new InputSystem_Actions();
    }

    void OnEnable()
    {
        input.UI.Enable();
        input.UI.Submit.performed += OnSubmit;
        input.UI.GameStop.performed += OnGameStop;
    }

    void OnDisable()
    {
        input.UI.Submit.performed -= OnSubmit;
        input.UI.GameStop.performed -= OnGameStop;
        input.UI.Disable();

        // 念のため：無効化されたまま timeScale=0 事故を防ぐ
        Time.timeScale = 1f;
    }

    void Start()
    {
        // 初期：タイトル
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;
        if (GameStopImage != null) GameStopImage.enabled = false;

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

    // --------------------
    // Input
    // --------------------
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

        // 一時停止中にSubmitで再開したいならここに書ける
        // if (state == State.Stop) ResumeFromStop();
    }

    void OnGameStop(InputAction.CallbackContext ctx)
    {
        if (transitioning) return;

        // Playing中だけポーズ可能（Title/GameOver中は無視）
        if (state == State.Playing)
        {
            EnterStop();
            return;
        }

        if (state == State.Stop)
        {
            ResumeFromStop();
            return;
        }
    }

    // --------------------
    // State transitions
    // --------------------
    void StartGame()
    {
        transitioning = true;
        state = State.Playing;

        Time.timeScale = 1f;

        if (TitleImage != null) TitleImage.enabled = false;
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (GameStopImage != null) GameStopImage.enabled = false;

        if (gameRoot != null) gameRoot.SetActive(true);

        // ★ここが重要：Spawnerを有効化して、SpawnLoopを再開する
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

        // ポーズUI表示
        if (GameStopImage != null) GameStopImage.enabled = true;

        // ゲーム停止（物理/アニメ/WaitForSeconds が止まる）
        Time.timeScale = 0f;

        // スポナーも安全のため止める（timeScaleでも止まるが二重に安全）
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

        // ポーズUI非表示
        if (GameStopImage != null) GameStopImage.enabled = false;

        // ゲーム再開
        Time.timeScale = 1f;

        // スポナー再開
        if (spawner != null)
        {
            spawner.enabled = true;
            spawner.StartSpawn();
        }

        Debug.Log("[State] Resume (Unpaused)");
    }

    void OnGameOver()
    {
        if (state != State.Playing) return;

        transitioning = true;
        state = State.GameOver;

        // 念のためポーズ解除
        Time.timeScale = 1f;

        // ゲーム停止（UIは止めない）
        if (spawner != null)
        {
            spawner.StopSpawn();
            spawner.enabled = false;
        }

        if (gameRoot != null) gameRoot.SetActive(false);

        if (GameStopImage != null) GameStopImage.enabled = false;

        StartCoroutine(GameOverSequence());
    }

    IEnumerator GameOverSequence()
    {
        if (GameOverImage != null) GameOverImage.enabled = true;

        // timeScaleに影響されない
        yield return new WaitForSecondsRealtime(1f);

        if (GameOverImage != null) GameOverImage.enabled = false;
        if (TitleImage != null) TitleImage.enabled = true;

        transitioning = false;
        Debug.Log("[State] GameOver -> Title (press Submit)");
    }

    void ShowTitle()
    {
        transitioning = true;
        state = State.Title;

        Time.timeScale = 1f;

        if (TitleImage != null) TitleImage.enabled = true;
        if (GameOverImage != null) GameOverImage.enabled = false;
        if (GameStopImage != null) GameStopImage.enabled = false;

        // タイトル中はゲーム停止
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
