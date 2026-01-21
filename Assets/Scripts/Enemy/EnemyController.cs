using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
    敵1体を管理するクラス

    ・弾との距離で当たり判定
    ・当たったら死亡（演出コルーチンへ）
    ・Spawner生成ならSpawner経由で消す（List管理を壊さない）
    ・死亡エフェクトはEffectManagerに通知して生成
    ・死亡した瞬間にスコア加算（ScoreManagerがあれば）
*/
public class EnemyController : MonoBehaviour
{
    //================
    // 当たり判定
    //================

    [SerializeField] private float HitRadius = 0.5f;

    [Tooltip("当たり判定の中心（未設定ならEnemy位置）")]
    [SerializeField] private Transform HitCenter;

    //================
    // Death
    //================

    [Tooltip("消えるまでの遅延時間（死亡アニメ用）")]
    [SerializeField] private float DestroyDelay = 0f;

    //================
    // Effect
    //================

    [Tooltip("未設定ならシーンから自動取得（無ければエラー）")]
    [SerializeField] private EffectManager EffectManager;

    //================
    // Score
    //================

    [Tooltip("未設定ならシーンから自動取得（無ければエラー）")]
    [SerializeField] private ScoreManager ScoreManager;

    //================
    // Animator
    //================

    [Tooltip("死亡アニメBoolパラメータ名（Animator側に無いなら空でもOK）")]
    [SerializeField] private string DeathBoolParam = "IsDeath";

    //================
    //敵死亡時演出
    //================

    [Tooltip("見た目のSpriteRenderer（未設定なら子から自動取得）")]
    [SerializeField] private SpriteRenderer TargetRenderer;

    [Tooltip("パーンで飛ぶ距離（Unity単位）")]
    [SerializeField] private float BurstDistance = 0.9f;

    [Tooltip("パーン時間（短いほど気持ちいい）")]
    [SerializeField] private float BurstTime = 0.18f;

    [Tooltip("回転量（度）")]
    [SerializeField] private float BurstRotate = 240f;

    [Tooltip("パーン完了後、エフェクトまでの待ち")]
    [SerializeField] private float AfterBurstWait = 0.05f;

    [Tooltip("破片を残す時間")]
    [SerializeField] private float PiecesLife = 0.25f;

    //================
    // SE (One Shot)
    //================

    [Tooltip("未設定なら子から自動取得（無ければ未使用）")]
    [SerializeField] private AudioSource SeSource;

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE（未使用でもOK）")]
    [SerializeField] private AudioClip HitOneShot;

    [Range(0f, 1f)]
    [SerializeField] private float HitOneShotVolume = 1.0f;

    //================
    // 生成管理
    //================

    Animator Anim;
    bool IsDead = false;

    /*
        Spawner管理
        Spawner生成の敵なら「Spawner経由で消す」ために保持する
    */
    EnemySpawner OwnerSpawner;
    GameObject MyInstance;

    Coroutine DeathRoutine;

    //======================================================
    /*
         Spawnerが敵生成直後に呼ぶ
         「Spawner管理下の敵」として覚えておく
    */
    //======================================================
    public void SetOwner(EnemySpawner Spawner, GameObject Instance)
    {
        OwnerSpawner = Spawner;
        MyInstance = Instance;
    }

    //================
    // Unity Event
    //================

    void Start()
    {
        // Animator を取得する
        Anim = GetComponentInChildren<Animator>();

        /*
            EffectManager 自動取得
            未設定ならシーンから探す
            それでも無ければエラー（エフェクトが出ないのは不具合なので）
        */
        if (EffectManager == null) EffectManager = FindFirstObjectByType<EffectManager>();
        if (EffectManager == null) Debug.LogError($"[Enemy] EffectManager がシーンに見つかりません name={name}");

        /*
            ScoreManager 自動取得
            未設定でも動くように探す
            それでも無ければエラー（スコアが加算されないのは仕様外なので）
        */
        if (ScoreManager == null) ScoreManager = FindFirstObjectByType<ScoreManager>();
        if (ScoreManager == null) Debug.LogError($"[Enemy] ScoreManager がシーンに見つかりません name={name}");

        /*
            分割演出用SpriteRenderer
            子階層からも拾えるよう true（非アクティブも対象）
        */
        if (TargetRenderer == null) TargetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (TargetRenderer == null) Debug.LogError($"[Enemy] SpriteRenderer が見つかりません（4分割できません） name={name}");

        /*
            SE用AudioSource
            無くても致命ではない（鳴らせないだけ）
        */
        if (SeSource == null) SeSource = GetComponentInChildren<AudioSource>(true);
        if (SeSource != null) SeSource.playOnAwake = false;
    }

    void Update()
    {
        if (IsDead) return;

        /*
            弾リストを参照して当たり判定
            Bullet.AllBullets が無い/空なら何もしない
        */
        List<Bullet> Bullets = Bullet.AllBullets;
        if (Bullets == null || Bullets.Count == 0) return;

        /*
            当たり判定の中心
            HitCenterがあればそこ
            無ければEnemy自身の位置
        */
        Transform Tr = transform;

        Vector3 Center = Tr.position;
        if (HitCenter != null) Center = HitCenter.position;

        float RadiusSq = HitRadius * HitRadius;

        /*
            後ろから回す
            弾側が途中でDestroy/Removeされても事故りにくい
        */
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            Bullet bt = Bullets[i];
            if (bt == null) continue;

            Vector3 BulletPos = bt.transform.position;
            float Sq = (BulletPos - Center).sqrMagnitude;

            /*
                当たったら死亡
                そのフレームで複数判定される必要が無いので break
            */
            if (Sq <= RadiusSq)
            {
                Die(Tr.position);
                break;
            }
        }
    }

    //================
    // Death Start
    //================

    /*
        死亡開始

        ・多重実行を防ぐ（IsDead）
        ・スコア加算は「倒された瞬間」に1回だけ
        ・演出はコルーチンで順番に処理する
    */
    void Die(Vector3 DiePos)
    {
        if (IsDead) return;
        IsDead = true;

        /*
            スコア加算（敵が倒された瞬間）
            ScoreManager が無いなら加算できないので何もしない（Startでエラーは出る）
        */
        if (ScoreManager != null) ScoreManager.AddKillScore();

        /*
            敵被弾SEはマネージャーに任せる（居れば）
        */
        if (EnemyHitSEManager.Instance != null) EnemyHitSEManager.Instance.PlayEnemyHit();

        // この敵自身のSEを鳴らしたい場合（未使用でもOK）
        // PlayHitOneShot(DiePos);

        if (DeathRoutine != null) StopCoroutine(DeathRoutine);
        DeathRoutine = StartCoroutine(DeathSequence(DiePos));
    }

    //================
    // SE
    //================

    /*
        この敵のAudioSourceで鳴らす単発SE
        （いまはEnemyHitSEManager側で鳴らす想定なので未使用でもOK）
    */
    void PlayHitOneShot(Vector3 Pos)
    {
        if (HitOneShot == null) return;

        if (SeSource == null)
        {
            AudioSource.PlayClipAtPoint(HitOneShot, Pos, HitOneShotVolume);
            return;
        }

        SeSource.PlayOneShot(HitOneShot, HitOneShotVolume);
    }

    //================
    // Death Sequence
    //================

    /*
        死亡演出の流れ

        1) 当たり判定停止
        2) 死亡アニメ（あれば）
        3) 4分割生成 → パーン → エフェクト → 破片消し
        4) 本体削除（Spawner経由 or Destroy）
    */
    IEnumerator DeathSequence(Vector3 DiePos)
    {
        //================
        // 1) 当たり判定停止
        //================

        /*
            事故防止：当たり判定停止
            演出中にさらに当たると処理が乱れる
        */
        DisableColliders();

        //================
        // 2) 死亡アニメ
        //================

        /*
            死亡アニメ（使うなら）
            Boolパラメータが無い環境でも落ちないように存在チェックをする
        */
        ApplyDeathAnimation();

        //================
        // 3) 4分割の準備
        //================

        /*
            Spriteが無い場合は分割演出をせず
            すぐエフェクト → 削除へ
        */
        if (!CanSplitSprite())
        {
            SpawnDeathEffect(DiePos);
            KillSelf();
            yield break;
        }

        /*
            カットした4分割を生成
            Atlas/Packing環境でも崩れにくい（textureRectを使う）
        */
        GameObject PiecesRoot;
        Transform[] Pieces;
        Vector3[] BaseLocalPos;

        bool DidSplit = TrySpawnRuntimePiecesRealCut(TargetRenderer, out PiecesRoot, out Pieces, out BaseLocalPos);
        if (!DidSplit)
        {
            SpawnDeathEffect(DiePos);
            KillSelf();
            yield break;
        }

        /*
            本体を隠す（Rendererだけ消す）
            破片が見えるように本体を消しておく
        */
        TargetRenderer.enabled = false;

        //================
        // 3-1) パーン演出
        //================

        yield return StartCoroutine(BurstPieces(Pieces, BaseLocalPos));

        //================
        // 3-2) エフェクト
        //================

        /*
            少し待ってからエフェクト
            パーン直後に出すと見えづらいので遅らせられるようにしている
        */
        if (AfterBurstWait > 0f) yield return new WaitForSeconds(AfterBurstWait);
        SpawnDeathEffect(DiePos);

        //================
        // 3-3) 破片の後始末
        //================

        /*
            破片を少し残す
            すぐ消すと気持ちよさが減るので残せるようにしている
        */
        if (PiecesLife > 0f) yield return new WaitForSeconds(PiecesLife);

        if (PiecesRoot != null) Destroy(PiecesRoot);

        //================
        // 4) 本体削除
        //================

        KillSelf();
    }

    //================
    // 補助：Collider演出中にもう一度当たった判定しないための予防線を貼る
    //================

    void DisableColliders()
    {
        // 3D Collider を無効化する
        Collider Col3d = GetComponent<Collider>();
        if (Col3d != null) Col3d.enabled = false;

        // 2D Collider を無効化する
        Collider2D Col2d = GetComponent<Collider2D>();
        if (Col2d != null) Col2d.enabled = false;
    }

    //================
    // 補助：Death Animation
    //================

    void ApplyDeathAnimation()
    {
        if (Anim == null) return;

        // パラメータ名が空なら何もしない
        if (string.IsNullOrEmpty(DeathBoolParam)) return;

        // 指定Boolが存在する場合だけ SetBool する（警告防止）
        if (HasBoolParameter(Anim, DeathBoolParam)) Anim.SetBool(DeathBoolParam, true);
    }

    //================
    // 補助：Effect
    //================

    void SpawnDeathEffect(Vector3 Pos)
    {
        if (EffectManager != null) EffectManager.PlayEffect(Pos);
    }

    //================
    // 補助：Split可否
    //================

    bool CanSplitSprite()
    {
        if (TargetRenderer == null) return false;

        Sprite sp = TargetRenderer.sprite;
        if (sp == null) return false;

        Texture2D Tex = sp.texture;
        if (Tex == null) return false;

        return true;
    }

    //================
    // Burst Pieces
    //================

    /*
        4分割破片のパーン演出

        ・4方向に飛ばす
        ・回転も加える
        ・easeOutCubicで「最初速く、最後ゆっくり」にして気持ちよくする
    */
    IEnumerator BurstPieces(Transform[] Pieces, Vector3[] BaseLocalPos)
    {
        if (Pieces == null) yield break;
        if (BaseLocalPos == null) yield break;
        if (Pieces.Length < 4) yield break;
        if (BaseLocalPos.Length < 4) yield break;

        Vector3[] Dirs =
        {
            new Vector3(-1f,  1f, 0f).normalized,
            new Vector3( 1f,  1f, 0f).normalized,
            new Vector3(-1f, -1f, 0f).normalized,
            new Vector3( 1f, -1f, 0f).normalized,
        };

        Quaternion[] BaseRot = new Quaternion[4];
        for (int i = 0; i < 4; i++) BaseRot[i] = Pieces[i].localRotation;

        float Dur = Mathf.Max(0.0001f, BurstTime);
        float t = 0f;

        while (t < Dur)
        {
            t += Time.deltaTime;

            float Rate = t / Dur;
            if (Rate < 0f) Rate = 0f;
            if (Rate > 1f) Rate = 1f;

            float Ease = 1f - Mathf.Pow(1f - Rate, 3f);

            for (int i = 0; i < 4; i++)
            {
                Pieces[i].localPosition = BaseLocalPos[i] + Dirs[i] * (BurstDistance * Ease);

                float Sign = 1f;
                if (i % 2 != 0) Sign = -1f;

                Pieces[i].localRotation = BaseRot[i] * Quaternion.Euler(0f, 0f, BurstRotate * Sign * Ease);
            }

            yield return null;
        }
    }

    //================
    // Runtime 4-split (Real Cut)
    //================

    /*
        Spriteを「textureRect」で本当に4分割して生成する

        ・Packed/Atlas環境でもズレにくい
        ・4つのSpriteRendererを子として生成する
        ・BurstPiecesで動かしやすいように root + 4つのpieceを返す
    */
    bool TrySpawnRuntimePiecesRealCut(
        SpriteRenderer Src,
        out GameObject Root,
        out Transform[] Pieces,
        out Vector3[] BaseLocalPos
    )
    {
        Root = null;
        Pieces = null;
        BaseLocalPos = null;

        if (Src == null) return false;

        Sprite sp = Src.sprite;
        if (sp == null) return false;

        Texture2D Tex = sp.texture;
        if (Tex == null) return false;

        Rect tr = sp.textureRect;

        float HalfW = tr.width * 0.5f;
        float HalfH = tr.height * 0.5f;

        Rect[] Rects =
        {
            new Rect(tr.xMin,         tr.yMin,         HalfW, HalfH),
            new Rect(tr.xMin + HalfW, tr.yMin,         HalfW, HalfH),
            new Rect(tr.xMin,         tr.yMin + HalfH, HalfW, HalfH),
            new Rect(tr.xMin + HalfW, tr.yMin + HalfH, HalfW, HalfH),
        };

        Vector2 Pivot = new Vector2(0.5f, 0.5f);
        float Ppu = sp.pixelsPerUnit;

        Root = new GameObject($"{Src.gameObject.name}_Pieces");
        Root.transform.SetParent(Src.transform, false);
        Root.transform.localPosition = Vector3.zero;
        Root.transform.localRotation = Quaternion.identity;
        Root.transform.localScale = Vector3.one;

        Pieces = new Transform[4];
        BaseLocalPos = new Vector3[4];

        Vector3 Ext = sp.bounds.extents;

        Vector3[] Offsets =
        {
            new Vector3(-Ext.x * 0.5f, -Ext.y * 0.5f, 0f),
            new Vector3( Ext.x * 0.5f, -Ext.y * 0.5f, 0f),
            new Vector3(-Ext.x * 0.5f,  Ext.y * 0.5f, 0f),
            new Vector3( Ext.x * 0.5f,  Ext.y * 0.5f, 0f),
        };

        /*
            BurstPiecesの方向配列は「LU,RU,LD,RD」順で動かすので
            生成した4分割の順番を合わせるためにマップする
        */
        int[] Map = { 2, 3, 0, 1 };

        int SortingLayerId = Src.sortingLayerID;
        int SortingOrder = Src.sortingOrder;

        for (int i = 0; i < 4; i++)
        {
            int Idx = Map[i];

            Sprite PieceSprite;
            try
            {
                PieceSprite = Sprite.Create(Tex, Rects[Idx], Pivot, Ppu, 0, SpriteMeshType.FullRect);
            }
            catch
            {
                if (Root != null) Destroy(Root);
                Root = null;
                return false;
            }

            GameObject Go = new GameObject($"Piece_{i}");
            //transformをここでていぎしておけば下のtransformを定義しないで済む
            Go.transform.SetParent(Root.transform, false);
            Go.transform.localPosition = Offsets[Idx];
            Go.transform.localRotation = Quaternion.identity;
            Go.transform.localScale = Vector3.one;

            //srを三回定義しているから無駄な処理が走っている
            SpriteRenderer sr = Go.AddComponent<SpriteRenderer>();
            sr.sprite = PieceSprite;
            sr.sortingLayerID = SortingLayerId;
            sr.sortingOrder = SortingOrder + 1;

            sr.color = Src.color;
            sr.sharedMaterial = Src.sharedMaterial;
            sr.flipX = Src.flipX;
            sr.flipY = Src.flipY;

            Pieces[i] = Go.transform;
            BaseLocalPos[i] = Go.transform.localPosition;
        }

        return true;
    }

    //================
    // Kill
    //================

    /*
        敵を消す

        ・Spawner管理下なら Spawner.KillSpawned でリストからも消す
        ・それ以外はDestroy
    */
    void KillSelf()
    {
        // Spawner管理なら Spawner 経由で消す
        if (OwnerSpawner != null && MyInstance != null)
        {
            OwnerSpawner.KillSpawned(MyInstance, DestroyDelay);
            return;
        }

        // 管理外なら通常Destroy
        Destroy(transform.root.gameObject, DestroyDelay);
    }

    //================
    // Animator
    //================

    /*
        Animatorに指定Boolパラメータが存在するかチェック
        無いパラメータにSetBoolすると警告が出るので保険
    */
    bool HasBoolParameter(Animator Animator, string ParamName)
    {
        foreach (var p in Animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.name == ParamName) return true;
        }

        return false;
    }
}
