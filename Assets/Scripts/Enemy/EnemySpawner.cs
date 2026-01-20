using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵を一定時間ごとに生成・管理するクラス
/// ・スポーン開始 / 停止が可能
/// ・生成した敵をリストで保持
/// ・ゲームオーバーやリトライ時の制御に対応
///
/// 【このクラスで使っている仕組みメモ
/// ・List<T> で「生成した敵の参照」を保持する
/// ・Coroutine + IEnumerator で「時間経過する処理（一定間隔スポーン）」を書く
/// ・Camera.main / Random.Range で「画面内のランダム位置」を作る
/// ・Destroyされた参照がListに残る問題を CleanupList() で解決する　敵を消しても参照した情報が破棄されないようにする
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public float appearanceTime = 3f;     // 敵を生成する間かく
    public GameObject enemyPrefab;        // 生成する敵Prefab
    public Transform player;              // スポーン位置計算用（プレイヤー基準でこのくらい離れているところに生成とする）

    // 現在スポーン中の敵インスタンス一覧
    // 【List<GameObject>】
    // ・生成した敵の参照を保持しておくためのコレクション
    // ・後で「全削除」「特定敵の削除」「敵一覧参照」などに使う

    readonly List<GameObject> spawned = new List<GameObject>();

    // スポーン処理用コルーチン
    // ・StartCoroutine の戻り値を保存しておくと StopCoroutine できる
    // ・「いまスポーン中か？」のフラグとしても使える（nullなら停止中）
    Coroutine spawnRoutine;

    // ======================
    // Spawn SE
    // ======================
    [Header("Spawn SE")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（無ければ自動追加）")]
    public AudioSource seSource;

    [Tooltip("敵が登場した瞬間に鳴らすSE")]
    public AudioClip LaunchSE;

    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("この秒数以内の連続再生は無視（多重防止）")]
    public float minInterval = 0.05f;

    // 前回SEを鳴らした時間（Time.time）を記録して連打防止に使う
    float lastPlayTime = -999f;

    /// <summary>
    /// 初期化処理
    /// AudioSourceが未設定の場合は自動で取得または追加し、SE再生用の設定を行う
    /// </summary>
    void Start()
    {
        // AudioSource取得

        if (seSource == null)
        {
            seSource = GetComponent<AudioSource>();
        }
        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
        }

        // SE用途なので自動再生やループを無効化
        seSource.playOnAwake = false;
        seSource.loop = false;
    }

    /// <summary>
    /// 毎フレーム呼ばれる
    /// Destroyされた敵がリストに残らないようにリストをクリーンアップする
    /// </summary>
    void Update()
    {
        // Destroyされた敵がリストに残らないよう毎フレーム掃除
        // 【なぜ必要？】
        // ・Destroy(e) しても List からは自動で消えない
        // ・敵が自滅/画面外で消える等、Spawnerを経由せずDestroyされるケースでもListが汚れないようにする
        CleanupList();
    }

    // =========================================================
    // 外から受け取る用
    // =========================================================

    /// <summary>
    /// 現在生成されている敵一覧を取得　　敵を読み取る
    /// </summary>
    public IReadOnlyList<GameObject> GetSpawnedEnemies()
    {
        // 【IReadOnlyList】
        // ・外部から Add/Remove できない形で公開する（改変事故を防ぐ）
        // ・Spawner以外は「読むだけ」にしたい
        return spawned;
    }

    /// <summary>
    /// 敵のスポーンを開始する
    /// 既にスポーン中の場合は何もしない
    /// 必要な参照が揃っているかチェックし、問題なければコルーチンでスポーンループを開始する
    /// </summary>
    public void StartSpawn()
    {
        // すでに動いていたら二重起動しない
        // 二重起動すると
        // ＞SpawnLoopが2本走る→敵が2倍ペースで出るから✖
        if (spawnRoutine != null) return;

        // 参照チェック（落ちる原因を事前に潰す）　参照があるか確認する

        if (enemyPrefab == null)
        {
            Debug.LogError("[Spawner] enemyPrefab が未設定です");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[Spawner] player が未設定です");
            return;
        }

        if (Camera.main == null)
        {
            Debug.LogError("[Spawner] Camera.main が取れません（MainCameraタグ確認）");
            return;
        }

        // スポーンループ開始
        spawnRoutine = StartCoroutine(SpawnLoop());
        Debug.Log("[Spawner] StartSpawn");
    }

    /// <summary>
    /// 敵のスポーンを停止する
    /// スポーン中のコルーチンを停止し、以降敵が生成されなくなる
    /// </summary>
    public void StopSpawn()
    {
        if (spawnRoutine != null)
        {
            // 【StopCoroutine】
            // ・SpawnLoopの実行を止める＝以降スポーンされない
            StopCoroutine(spawnRoutine);
            spawnRoutine = null; // nullに戻す＝「停止中」という状態を明確化
            Debug.Log("[Spawner] StopSpawn");
        }
    }

    // =========================================================
    // スポーン処理本体
    // =========================================================

    /// <summary>
    /// 敵を一定間隔で生成し続けるコルーチン
    /// 参照切れやPrefab未設定時は一時停止し、復帰を待つ
    /// 生成した敵はリストに追加し、SE再生や所有者設定も行う
    /// </summary>
    IEnumerator SpawnLoop()
    {
        int loop = 0;

        while (true)
        {
            loop++;

            if (enemyPrefab == null || player == null || Camera.main == null)
            {
                Debug.LogWarning($"[Spawner] Loop={loop} missing refs. wait...");

                yield return new WaitForSeconds(1f);
                continue;
            }

            // スポーン位置を計算
            Vector3 pos = GetSpawnPosition(Camera.main);

            // 敵生成
            GameObject e = Instantiate(enemyPrefab, pos, Quaternion.identity);

            // 管理リストに追加
            // ・生成した敵を追跡して、後で削除/参照できるようにする
            spawned.Add(e);

            // 敵登場SE（生成した瞬間に鳴らす）
            PlayLaunchSE();

            // EnemyController があれば Spawner を所有者として渡す
            // 【GetComponentInChildren(true)】
            // ・子階層も含めて探す
            // ・true：非アクティブな子も対象にする（Prefab構造が変わっても拾えるよう保険）
            var ec = e.GetComponentInChildren<EnemyController>(true);
            if (ec != null)
                ec.SetOwner(this, e); // Spawner参照を渡して「倒された時にSpawnerへ通知」等ができる
            else
                Debug.LogError($"[Spawner] Spawned '{e.name}' has NO EnemyController. Prefabに付けてください。");

            // 次の生成まで待つ
            // ・これで一定間隔にする
            // ・appearanceTime秒待ってからwhileループの先頭に戻り、次を生成する
            yield return new WaitForSeconds(appearanceTime);
        }
    }

    /// <summary>
    /// 敵登場時のSEを再生する
    /// 多重再生防止のため、一定間隔未満では再生しない
    /// AudioSourceが未設定の場合は一時的にPlayClipAtPointで再生
    /// </summary>
    void PlayLaunchSE()
    {
        if (LaunchSE == null) return;

        // 多重再生防止
        // ・前回再生時間との差で「連打かどうか」を判定できる
        if (Time.time - lastPlayTime < minInterval)
            return;

        lastPlayTime = Time.time;

        if (seSource == null)
        {
            // PlayClipAtPoint
            // その場で一時AudioSourceを作って鳴らす
            AudioSource.PlayClipAtPoint(LaunchSE, transform.position, volume);
            return;
        }
        seSource.PlayOneShot(LaunchSE, volume);
    }

    // =========================================================
    // 敵の削除
    // =========================================================

    /// <summary>
    /// 指定した敵インスタンスをリストから削除し、Destroyする
    /// 遅延時間を指定可能
    /// </summary>
    public void KillSpawned(GameObject enemyInstance, float delay = 0f)
    {
        if (enemyInstance == null) return;

        // 管理リストから外す
        // ・DestroyするだけだとListには参照が残るため、先にRemoveしている
        spawned.Remove(enemyInstance);

        // ・delayを指定して一定時間後に削除できる
        if (delay <= 0f) Destroy(enemyInstance);
        else Destroy(enemyInstance, delay);
    }

    /// <summary>
    /// 生成済みの全ての敵を削除し、リストもクリアする
    /// タイトル戻りやリトライ時に呼び出す
    /// </summary>
    public void ClearAllSpawned()
    {
        // 【後ろからfor】
        // ・要素を削除/Destroyする可能性がある時は、後ろから回すと安全なことが多い
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
        spawned.Clear(); // List自体も空にする＝「今敵はいない」状態を明確化
    }

    /// <summary>
    /// Destroy済みの敵オブジェクトをリストから除去する
    /// Updateで毎フレーム呼ばれる
    /// </summary>
    void CleanupList()
    {
        // 後ろから消す
        // ・RemoveAtするとインデックスが詰まるので、前からだと飛ばしが起きる
        // ・後ろからにして安全に消す
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] == null)
                spawned.RemoveAt(i);
        }
    }

    // =========================================================
    // スポーン位置計算
    // =========================================================

    /// <summary>
    /// カメラ範囲内のランダムな位置を計算して返す
    /// X座標は画面幅内ランダム、Y座標はプレイヤーより上側でランダム
    /// </summary>
    Vector3 GetSpawnPosition(Camera cam)
    {
        // 【orthographicSize】
        // ・2D(正投影)カメラの「縦方向の半分のサイズ（ワールド単位）」
        float h = cam.orthographicSize;

        // 【aspect】
        // ・画面の横/縦比率
        // 横幅半分 = 縦半分(h) * aspect
        float w = h * cam.aspect;

        // カメラ中心Xを基準に左右へランダムに出す
        float cx = cam.transform.position.x;

        // 【Random.Range】
        // ・範囲内の乱数を返すUnityの基本
        // X：画面内のどこか
        float x = Random.Range(cx - w, cx + w);

        // Yプレイヤーより上（上から降ってくる/視界に入ってくる演出）
        float y = player.position.y + Random.Range(0f, h);

        // z=0で2D平面上に固定
        return new Vector3(x, y, 0f);
    }
}
