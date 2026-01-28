using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
     敵1体を管理するクラス

     【主な役割】
     ・弾との距離で当たり判定を行う（Collider依存しない）
     ・命中したら死亡（演出コルーチンへ）
     ・Spawner生成の敵はSpawner経由で削除する（管理リストを壊さない）
     ・死亡エフェクトはEffectManagerに通知して生成する
     ・死亡した瞬間にスコアを加算する（ScoreManagerがあれば）

     【当たり判定の設計】
     ・敵の当たり半径（HitRadius）＋ 弾の当たり半径（Bullet.GetHitRadius()）で判定する
     ・弾がチャージで大きくなった場合、弾側の半径を大きくしておけば当たり判定も大きくなる

     【設計方針】
     ・transform参照はキャッシュして、Update内で何度も呼ばない
     ・死亡処理は1回だけ実行されるようにする（多重実行防止）
     ・複雑な演出（Split）は工程ごとにコメントで意図を残す
*/
public sealed class EnemyController : MonoBehaviour
{
    //================
    // Hit
    //================

    [SerializeField] private float HitRadius = 0.5f;

    [Tooltip("当たり判定の中心（未設定なら敵自身）")]
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
    // Burst (Split演出)
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
    // SE
    //================

    [Tooltip("未設定なら子から自動取得（無ければ未使用）")]
    [SerializeField] private AudioSource SeSource;

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE（未使用でもOK）")]
    [SerializeField] private AudioClip HitOneShot;

    [Range(0f, 1f)]
    [SerializeField] private float HitOneShotVolume = 1.0f;

    //================
    // Cache
    //================

    /*
         この敵自身のTransformを保持する

         ・Updateで毎フレーム参照するためキャッシュする
         ・transformプロパティを何度も呼ばない（可読性と統一）
    */
    private Transform EnemyTransform;

    /*
         当たり判定の中心Transformを保持する

         ・HitCenterが設定されていればそれを使う
         ・未設定ならEnemyTransformを中心とする
         ・Update内で毎回「HitCenterがあるか」を判定しないための設計
    */
    private Transform HitCenterTransform;

    /*
         Colliderキャッシュ

         ・死亡演出開始時に当たり判定を止めるために使う
         ・GetComponentを死亡時に何度も呼ばない
    */
    private Collider Col3d;
    private Collider2D Col2d;

    /*
         Animatorキャッシュ

         ・死亡アニメがある場合にだけ使う
         ・無い環境でも落ちない設計にしている
    */
    private Animator Anim;

    //================
    // State
    //================

    private bool IsDead = false;
    private Coroutine DeathRoutine;

    //================
    // Spawner
    //================

    /*
         Spawner管理情報

         ・Spawnerが生成した敵ならOwnerSpawnerが入る
         ・削除時はSpawner経由で処理する
    */
    private EnemySpawner OwnerSpawner;
    private GameObject MyInstance;

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

    private void Awake()
    {
        // Transformをキャッシュ
        EnemyTransform = transform;

        /*
             当たり判定中心を確定する

             ・HitCenterがあればそこを中心にする
             ・無ければ敵自身を中心にする
             ・以降は HitCenterTransform.position を読むだけで済む
        */
        HitCenterTransform = EnemyTransform;
        if (HitCenter != null) HitCenterTransform = HitCenter;

        // Colliderをキャッシュ（無い場合もあるのでnull許容）
        Col3d = GetComponent<Collider>();
        Col2d = GetComponent<Collider2D>();
    }

    private void Start()
    {
        // Animator取得
        Anim = GetComponentInChildren<Animator>();

        /*
             EffectManager 自動取得

             ・未設定ならシーンから探す
             ・無い場合は演出が出ないためエラーを出す
        */
        if (EffectManager == null) EffectManager = FindFirstObjectByType<EffectManager>();
        if (EffectManager == null) Debug.LogError($"[Enemy] EffectManager がシーンに見つかりません name={name}");

        /*
             ScoreManager 自動取得

             ・未設定でも動くように探す
             ・無い場合はスコア加算ができないためエラーを出す
        */
        if (ScoreManager == null) ScoreManager = FindFirstObjectByType<ScoreManager>();
        if (ScoreManager == null) Debug.LogError($"[Enemy] ScoreManager がシーンに見つかりません name={name}");

        /*
             分割演出用SpriteRenderer

             ・未設定なら子階層から探す
             ・無い場合は分割演出ができないのでエラーを出す
        */
        if (TargetRenderer == null) TargetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (TargetRenderer == null) Debug.LogError($"[Enemy] SpriteRenderer が見つかりません（4分割できません） name={name}");

        /*
             SE用AudioSource

             ・無くても致命ではない（鳴らせないだけ）
        */
        if (SeSource == null) SeSource = GetComponentInChildren<AudioSource>(true);
        if (SeSource != null) SeSource.playOnAwake = false;
    }

    private void Update()
    {
        if (IsDead) return;

        /*
             弾リストを参照して当たり判定

             ・Bullet.AllBullets が無い/空なら何もしない
             ・当たり判定は距離で行う（Collider依存しない）
        */
        List<Bullet> Bullets = Bullet.AllBullets;
        if (Bullets == null || Bullets.Count == 0) return;

        //================
        // Hit Check
        //================

        /*
             当たり判定中心座標

             ・HitCenterTransform はAwakeで確定済み
             ・Update内で毎回中心を分岐しない
        */
        Vector3 Center = HitCenterTransform.position;

        /*
             敵側の半径
             弾側半径と足して判定する
        */
        float EnemyRadius = HitRadius;

        /*
             後ろから回す

             ・弾側が途中でDestroy/Removeされても事故りにくい
             ・命中した瞬間にDieへ移行する
        */
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            Bullet Bt = Bullets[i];
            if (Bt == null) continue;

            /*
                 Bullet側transform参照をローカルに保持する

                 ・Bt.transform.position を何度も書かない（可読性）
                 ・transformアクセス回数を減らす（軽量化）
            */
            Transform BulletTransform = Bt.transform;

            Vector3 BulletPos = BulletTransform.position;
            float Sq = (BulletPos - Center).sqrMagnitude;

            //================
            // Radius Merge
            //================

            /*
                 弾側の当たり半径を取得する

                 ・チャージ弾ならBt側が大きい半径を持っている想定
                 ・通常弾ならデフォルト半径のまま
            */
            float BulletRadius = Bt.GetHitRadius();

            /*
                 判定半径を合成する

                 ・「敵の半径」＋「弾の半径」
                 ・球同士が触れたら命中、という考え方
            */
            float Sum = EnemyRadius + BulletRadius;
            float RadiusSq = Sum * Sum;

            /*
                 当たったら死亡

                 ・そのフレームで複数判定される必要が無いので break
            */
            if (Sq <= RadiusSq)
            {
                Die(EnemyTransform.position);
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
    private void Die(Vector3 DiePos)
    {
        if (IsDead) return;
        IsDead = true;

        /*
             スコア加算（倒された瞬間）

             ・ScoreManagerが無いなら加算できない
             ・Startでエラーを出しているので、ここでは落とさない
        */
        if (ScoreManager != null) ScoreManager.AddKillScore();

        /*
             敵被弾SEはマネージャー側に任せる
             （居れば鳴らす、居なければ何もしない）
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
    private void PlayHitOneShot(Vector3 Pos)
    {
        if (HitOneShot == null) return;

        /*
             SeSourceが無い場合は、ワンショットの簡易再生に逃がす
        */
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

         1) 当たり判定停止（演出中に再ヒットしない）
         2) 死亡アニメ（あれば）
         3) 4分割生成 → パーン → エフェクト → 破片消し
         4) 本体削除（Spawner経由 or Destroy）
    */
    private IEnumerator DeathSequence(Vector3 DiePos)
    {
        //================
        // 1) Hit Stop
        //================

        /*
             事故防止：当たり判定停止
             演出中にさらに当たると処理が乱れる
        */
        DisableColliders();

        //================
        // 2) Death Animation
        //================

        /*
             死亡アニメ（使うなら）
             Boolパラメータが無い環境でも落ちないように存在チェックする
        */
        ApplyDeathAnimation();

        //================
        // 3) Split Prepare
        //================

        /*
             Spriteが無い場合は分割演出を行わない

             ・分割できない環境でもゲームが止まらない設計
             ・エフェクトだけ出して本体削除へ
        */
        if (!CanSplitSprite())
        {
            SpawnDeathEffect(DiePos);
            KillSelf();
            yield break;
        }

        /*
             Spriteを実際に4分割して破片を生成する

             ・Packed/Atlas環境でも崩れにくいよう textureRect を使う
             ・生成に失敗した場合もゲームが止まらない設計にする
        */
        GameObject PiecesRoot;
        Transform[] Pieces;
        Vector3[] BaseLocalPos;

        bool DidSplit =
            TrySpawnRuntimePiecesRealCut(
                TargetRenderer,
                out PiecesRoot,
                out Pieces,
                out BaseLocalPos
            );

        if (!DidSplit)
        {
            SpawnDeathEffect(DiePos);
            KillSelf();
            yield break;
        }

        /*
             本体を隠す（Rendererだけ消す）

             ・本体をDestroyすると座標基準が崩れる可能性があるため
             ・破片は同じ座標系のまま動かしたいのでRendererだけ消す
        */
        TargetRenderer.enabled = false;

        //================
        // 3-1) Burst
        //================

        /*
             破片を4方向に飛ばす演出
             BurstPieces内で距離・回転・イージングを適用する
        */
        yield return StartCoroutine(BurstPieces(Pieces, BaseLocalPos));

        //================
        // 3-2) Effect
        //================

        /*
             少し待ってからエフェクト

             ・パーン直後に出すと見えづらい場合があるため
             ・調整できるようAfterBurstWaitを用意している
        */
        if (AfterBurstWait > 0f) yield return new WaitForSeconds(AfterBurstWait);
        SpawnDeathEffect(DiePos);

        //================
        // 3-3) Cleanup Pieces
        //================

        /*
             破片を少し残す

             ・すぐ消すと気持ちよさが減る
             ・残す時間をPiecesLifeで調整できる
        */
        if (PiecesLife > 0f) yield return new WaitForSeconds(PiecesLife);

        if (PiecesRoot != null) Destroy(PiecesRoot);

        //================
        // 4) Kill
        //================

        KillSelf();
    }

    //================
    // Collider Helper
    //================

    /*
         演出中にもう一度当たった判定しないための予防線
    */
    private void DisableColliders()
    {
        if (Col3d != null) Col3d.enabled = false;
        if (Col2d != null) Col2d.enabled = false;
    }

    //================
    // Animator Helper
    //================

    private void ApplyDeathAnimation()
    {
        if (Anim == null) return;

        // パラメータ名が空なら何もしない
        if (string.IsNullOrEmpty(DeathBoolParam)) return;

        // 指定Boolが存在する場合だけ SetBool する（警告防止）
        if (HasBoolParameter(Anim, DeathBoolParam)) Anim.SetBool(DeathBoolParam, true);
    }

    //================
    // Effect Helper
    //================

    private void SpawnDeathEffect(Vector3 Pos)
    {
        if (EffectManager != null) EffectManager.PlayEffect(Pos);
    }

    //================
    // Split Check
    //================

    /*
         4分割演出が可能か確認する

         ・SpriteRendererが無い
         ・Spriteが無い
         ・Textureが無い
         上記のいずれかなら分割できない
    */
    private bool CanSplitSprite()
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

         【挙動】
         ・4方向に飛ばす
         ・回転も加える
         ・easeOutCubicで「最初速く、最後ゆっくり」にする
    */
    private IEnumerator BurstPieces(Transform[] Pieces, Vector3[] BaseLocalPos)
    {
        if (Pieces == null) yield break;
        if (BaseLocalPos == null) yield break;
        if (Pieces.Length < 4) yield break;
        if (BaseLocalPos.Length < 4) yield break;

        /*
             4方向（左上／右上／左下／右下）
             normalizedで方向ベクトルとして使う
        */
        Vector3[] Dirs =
        {
            new Vector3(-1f,  1f, 0f).normalized,
            new Vector3( 1f,  1f, 0f).normalized,
            new Vector3(-1f, -1f, 0f).normalized,
            new Vector3( 1f, -1f, 0f).normalized,
        };

        /*
             元の回転を保存して、そこから回転を加算する
             （破片がすでに回転している環境でも破綻しにくい）
        */
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

            /*
                 easeOutCubic
                 最初が速く、最後がゆっくりになる
            */
            float Ease = 1f - Mathf.Pow(1f - Rate, 3f);

            for (int i = 0; i < 4; i++)
            {
                /*
                     位置：初期位置 + 方向 * 距離 * Ease
                     Easeにより「最初に勢いよく飛ぶ」見た目になる
                */
                Pieces[i].localPosition = BaseLocalPos[i] + Dirs[i] * (BurstDistance * Ease);

                /*
                     回転：左右で回転方向を変える
                     ・iが奇数のとき回転方向を反転する
                */
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
         Spriteを「textureRect」を使って実際に4分割して生成する

         【目的】
         ・死亡時に「割れる演出」を行うため、破片用Spriteを生成する

         【やっていること】
         1) 元SpriteのtextureRectを4分割する矩形を作る
         2) Sprite.Createで各矩形からSpriteを作る
         3) Rootを作って破片を子にぶら下げる
         4) 破片の初期位置（BaseLocalPos）を返してBurstで使う

         【注意】
         ・Packed/Atlas環境でもズレにくいよう textureRect を使う
         ・失敗した場合は false を返す（演出をスキップできる）
    */
    private bool TrySpawnRuntimePiecesRealCut(
        SpriteRenderer Src,
        out GameObject Root,
        out Transform[] Pieces,
        out Vector3[] BaseLocalPos
    )
    {
        Root = null;
        Pieces = null;
        BaseLocalPos = null;

        //================
        // Validate Source
        //================

        /*
             分割に必要な情報が揃っているかチェックする

             ・Srcが無い → 分割できない
             ・Spriteが無い → 分割できない
             ・Textureが無い → 分割できない
        */
        if (Src == null) return false;

        Sprite sp = Src.sprite;
        if (sp == null) return false;

        Texture2D Tex = sp.texture;
        if (Tex == null) return false;

        //================
        // Calculate Split Rects
        //================

        /*
             textureRectを使って4分割する矩形を作る

             ・textureRectは「テクスチャ内でこのSpriteが使っている領域」
             ・Atlas/Pack時でも正しい領域を切り出せる
        */
        Rect tr = sp.textureRect;

        float HalfW = tr.width * 0.5f;
        float HalfH = tr.height * 0.5f;

        /*
             4分割の矩形
             （左下／右下／左上／右上）の順で作る
        */
        Rect[] Rects =
        {
            new Rect(tr.xMin,         tr.yMin,         HalfW, HalfH),
            new Rect(tr.xMin + HalfW, tr.yMin,         HalfW, HalfH),
            new Rect(tr.xMin,         tr.yMin + HalfH, HalfW, HalfH),
            new Rect(tr.xMin + HalfW, tr.yMin + HalfH, HalfW, HalfH),
        };

        /*
             Sprite.Createに渡す基本情報
             ・Pivotは中心（0.5,0.5）
             ・Ppuは元Spriteの値を引き継ぐ（サイズがズレないように）
        */
        Vector2 Pivot = new Vector2(0.5f, 0.5f);
        float Ppu = sp.pixelsPerUnit;

        //================
        // Create Root
        //================

        /*
             破片をまとめるRootを生成する

             ・破片を一括でDestroyしやすくする
             ・Enemy配下に置くことで同じ座標系で動かせる
        */
        Root = new GameObject($"{Src.gameObject.name}_Pieces");

        Transform RootTransform = Root.transform;
        RootTransform.SetParent(Src.transform, false);
        RootTransform.localPosition = Vector3.zero;
        RootTransform.localRotation = Quaternion.identity;
        RootTransform.localScale = Vector3.one;

        Pieces = new Transform[4];
        BaseLocalPos = new Vector3[4];

        //================
        // Calculate Offsets
        //================

        /*
             破片の初期配置位置を計算する

             ・Spriteのbounds（ワールド単位）から大きさを取る
             ・中央から4方向に少しずらして配置する
        */
        Vector3 Ext = sp.bounds.extents;

        Vector3[] Offsets =
        {
            new Vector3(-Ext.x * 0.5f, -Ext.y * 0.5f, 0f),
            new Vector3( Ext.x * 0.5f, -Ext.y * 0.5f, 0f),
            new Vector3(-Ext.x * 0.5f,  Ext.y * 0.5f, 0f),
            new Vector3( Ext.x * 0.5f,  Ext.y * 0.5f, 0f),
        };

        /*
             BurstPiecesの方向配列は「左上・右上・左下・右下」順で動かす
             Rects/Offsetsは「左下・右下・左上・右上」順なので対応付けが必要
        */
        int[] Map = { 2, 3, 0, 1 };

        //================
        // Copy Sort Settings
        //================

        /*
             Sorting設定を元Rendererからコピーする

             ・破片は本体より前に出したいので sortingOrder を +1 する
        */
        int SortingLayerId = Src.sortingLayerID;
        int SortingOrder = Src.sortingOrder;

        //================
        // Create Pieces
        //================

        /*
             4つの破片を生成する

             ・Sprite.Createで切り出しSpriteを生成する
             ・Rootの子として配置する
             ・Renderer設定を元から引き継ぐ
             ・Burst用のTransform/初期位置を保存する
        */
        for (int i = 0; i < 4; i++)
        {
            int Idx = Map[i];

            //================
            // Create Sprite
            //================

            /*
                 指定矩形からSpriteを生成する

                 ・例外が出る環境があるため try/catch で安全に失敗させる
                 ・失敗したらRootを消してfalseを返す
            */
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

            //================
            // Create Object
            //================

            /*
                 破片オブジェクトを生成する

                 ・Transformはローカル変数に保持して何度も呼ばない
                 ・Root配下にして同じ座標系で扱う
            */
            GameObject Go = new GameObject($"Piece_{i}");

            Transform PieceTransform = Go.transform;
            PieceTransform.SetParent(RootTransform, false);
            PieceTransform.localPosition = Offsets[Idx];
            PieceTransform.localRotation = Quaternion.identity;
            PieceTransform.localScale = Vector3.one;

            //================
            // Create Renderer
            //================

            /*
                 SpriteRendererを付与して見た目を作る
            */
            SpriteRenderer Sr = Go.AddComponent<SpriteRenderer>();

            /*
                 Spriteを設定する
                 このSpriteが「切り出した破片の見た目」になる
            */
            Sr.sprite = PieceSprite;

            /*
                 Sortingを設定する

                 ・破片が本体の後ろに回り込まないようにする
                 ・本体より1つ手前に出す
            */
            Sr.sortingLayerID = SortingLayerId;
            Sr.sortingOrder = SortingOrder + 1;

            /*
                 元Rendererの見た目設定を引き継ぐ

                 ・本体と破片の見た目がズレないようにするため
                 ・色／マテリアル／反転状態をコピーする
            */
            Sr.color = Src.color;                     // Tint / Alpha（フェード・赤点滅なども引き継ぐ）
            Sr.sharedMaterial = Src.sharedMaterial;   // Shader / Material（アウトライン等を崩さない）
            Sr.flipX = Src.flipX;                     // 左右反転（向きを維持）
            Sr.flipY = Src.flipY;                     // 上下反転（特殊演出対応）

            //================
            // Save For Burst
            //================

            /*
                 BurstPiecesで動かすための情報を保存する

                 ・Pieces        : 動かす対象Transform
                 ・BaseLocalPos  : パーン開始前の初期位置（基準）
            */
            Pieces[i] = PieceTransform;
            BaseLocalPos[i] = PieceTransform.localPosition;
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
    private void KillSelf()
    {
        // Spawner管理ならSpawner経由で消す
        if (OwnerSpawner != null && MyInstance != null)
        {
            OwnerSpawner.KillSpawned(MyInstance, DestroyDelay);
            return;
        }

        // 管理外なら通常Destroy
        Destroy(EnemyTransform.root.gameObject, DestroyDelay);
    }

    //================
    // Animator Helper
    //================

    /*
         Animatorに指定Boolパラメータが存在するかチェック
         無いパラメータにSetBoolすると警告が出るので保険
    */
    private bool HasBoolParameter(Animator Animator, string ParamName)
    {
        foreach (var p in Animator.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Bool) continue;
            if (p.name == ParamName) return true;
        }

        return false;
    }
}
