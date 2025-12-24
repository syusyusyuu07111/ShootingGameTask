using UnityEngine;
using System.Collections;

/// <summary>
/// 敵1体を管理するクラス
/// ・弾との距離で当たり判定
/// ・当たったら死亡
/// ・Spawner生成ならSpawner経由で消す
/// ・死亡エフェクトはEffectManagerに通知して生成
/// </summary>
public class EnemyController : MonoBehaviour
{
    [Header("Hit Circle Settings")]
    public float hitRadius = 0.5f;

    [Tooltip("当たり判定の中心（未設定ならEnemy位置）")]
    public Transform hitCenter;

    [Header("Death")]
    [Tooltip("消えるまでの遅延時間（死亡アニメ用）")]
    public float destroyDelay = 0f;

    [Header("Effect")]
    [Tooltip("未設定でもOK（自動でシーンから探す）")]
    public EffectManager effectManager;

    [Header("Animator")]
    [Tooltip("死亡アニメBoolパラメータ名（Animator側に無いなら空でもOK）")]
    public string deathBoolParam = "IsDeath";

    [Header("Break Runtime (REAL CUT 4-split)")]
    [Tooltip("見た目のSpriteRenderer（未設定なら子から自動取得）")]
    public SpriteRenderer targetRenderer;

    [Tooltip("パーンで飛ぶ距離（Unity単位）")]
    public float burstDistance = 0.9f;

    [Tooltip("パーン時間（短いほど気持ちいい）")]
    public float burstTime = 0.18f;

    [Tooltip("回転量（度）")]
    public float burstRotate = 240f;

    [Tooltip("パーン完了後、エフェクトまでの待ち")]
    public float afterBurstWait = 0.05f;

    [Tooltip("破片を残す時間")]
    public float piecesLife = 0.25f;

    private Animator anim;
    private bool isDead = false;

    // Spawner管理
    private EnemySpawner ownerSpawner;
    private GameObject myInstance;

    Coroutine deathRoutine;

    // ======================
    // SE (One Shot)
    // ======================
    [Header("SE (One Shot)")]
    [Tooltip("未設定ならこのオブジェクトから自動取得（子でもOK）")]
    public AudioSource seSource;

    [Tooltip("被弾（死亡）した瞬間に鳴らすSE")]
    public AudioClip hitOneShot;

    [Range(0f, 1f)]
    public float hitOneShotVolume = 1.0f;

    public void SetOwner(EnemySpawner spawner, GameObject instance)
    {
        ownerSpawner = spawner;
        myInstance = instance;
    }

    void Start()
    {
        anim = GetComponentInChildren<Animator>();

        if (effectManager == null)
            effectManager = FindFirstObjectByType<EffectManager>();

        if (effectManager == null)
            Debug.LogError($"[Enemy] EffectManager がシーンに見つかりません name={name}");

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (targetRenderer == null)
            Debug.LogError($"[Enemy] SpriteRenderer が見つかりません（4分割できません） name={name}");

        // SE Source 自動取得
        if (seSource == null)
            seSource = GetComponentInChildren<AudioSource>(true);

        if (seSource != null)
            seSource.playOnAwake = false;
    }

    void Update()
    {
        if (isDead) return;

        var bullets = Bullet.AllBullets;
        if (bullets == null || bullets.Count == 0) return;

        Vector3 center = (hitCenter != null) ? hitCenter.position : transform.position;
        float rSq = hitRadius * hitRadius;

        for (int i = bullets.Count - 1; i >= 0; i--)
        {
            Bullet b = bullets[i];
            if (b == null) continue;

            Vector3 bp = b.transform.position;
            float sq = (bp - center).sqrMagnitude;

            if (sq <= rSq)
            {
                Die(transform.position);
                break;
            }
        }
    }

    void Die(Vector3 diePos)
    {
        if (isDead) return;
        isDead = true;

        //  敵被弾SEはマネージャーに任せる
        if (EnemyHitSEManager.Instance != null)
            EnemyHitSEManager.Instance.PlayEnemyHit();

        if (deathRoutine != null) StopCoroutine(deathRoutine);
        deathRoutine = StartCoroutine(DeathSequence(diePos));
    }


    void PlayHitOneShot(Vector3 pos)
    {
        if (hitOneShot == null) return;

        if (seSource == null)
        {
            // AudioSourceが無い場合は一時的に鳴らす
            AudioSource.PlayClipAtPoint(hitOneShot, pos, hitOneShotVolume);
            return;
        }

        seSource.PlayOneShot(hitOneShot, hitOneShotVolume);
    }

    IEnumerator DeathSequence(Vector3 diePos)
    {
        // 事故防止：当たり判定停止（2D/3D両対応）
        var col3d = GetComponent<Collider>();
        if (col3d != null) col3d.enabled = false;

        var col2d = GetComponent<Collider2D>();
        if (col2d != null) col2d.enabled = false;

        // 死亡アニメ（使うなら）
        if (anim != null && !string.IsNullOrEmpty(deathBoolParam))
        {
            if (HasBoolParameter(anim, deathBoolParam))
                anim.SetBool(deathBoolParam, true);
        }

        // Spriteが無いなら従来：即エフェクト→消す
        if (targetRenderer == null || targetRenderer.sprite == null)
        {
            if (effectManager != null) effectManager.PlayEffect(diePos);
            KillSelf();
            yield break;
        }

        // ★ランタイムで「本当にカット」した4分割を生成
        bool didSplit = TrySpawnRuntimePiecesRealCut(
            targetRenderer,
            out GameObject piecesRoot,
            out Transform[] pieces,
            out Vector3[] baseLocalPos
        );

        if (!didSplit)
        {
            if (effectManager != null) effectManager.PlayEffect(diePos);
            KillSelf();
            yield break;
        }

        // 本体を隠す（Rendererだけ消す）
        targetRenderer.enabled = false;

        // パーン演出
        yield return StartCoroutine(BurstPieces(pieces, baseLocalPos));

        // 少し待ってエフェクト
        if (afterBurstWait > 0f)
            yield return new WaitForSeconds(afterBurstWait);

        if (effectManager != null)
            effectManager.PlayEffect(diePos);

        // 破片をちょい残す
        if (piecesLife > 0f)
            yield return new WaitForSeconds(piecesLife);

        if (piecesRoot != null)
            Destroy(piecesRoot);

        KillSelf();
    }

    IEnumerator BurstPieces(Transform[] pieces, Vector3[] baseLocalPos)
    {
        // 4方向（左上/右上/左下/右下）
        Vector3[] dirs =
        {
            new Vector3(-1f,  1f, 0f).normalized,
            new Vector3( 1f,  1f, 0f).normalized,
            new Vector3(-1f, -1f, 0f).normalized,
            new Vector3( 1f, -1f, 0f).normalized,
        };

        Quaternion[] baseRot = new Quaternion[4];
        for (int i = 0; i < 4; i++) baseRot[i] = pieces[i].localRotation;

        float dur = Mathf.Max(0.0001f, burstTime);
        float t = 0f;

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / dur);

            // easeOutCubic
            float ease = 1f - Mathf.Pow(1f - a, 3f);

            for (int i = 0; i < 4; i++)
            {
                pieces[i].localPosition = baseLocalPos[i] + dirs[i] * (burstDistance * ease);

                float sign = (i % 2 == 0) ? 1f : -1f;
                pieces[i].localRotation = baseRot[i] * Quaternion.Euler(0f, 0f, burstRotate * sign * ease);
            }

            yield return null;
        }
    }

    /// <summary>
    /// SpriteRendererのSpriteを「textureRect」で本当に4分割して生成する
    /// Atlas/Packing でも切り出しが崩れにくい版
    /// </summary>
    bool TrySpawnRuntimePiecesRealCut(
        SpriteRenderer src,
        out GameObject root,
        out Transform[] pieces,
        out Vector3[] baseLocalPos
    )
    {
        root = null;
        pieces = null;
        baseLocalPos = null;

        Sprite sp = src.sprite;
        if (sp == null) return false;

        Texture2D tex = sp.texture;
        if (tex == null) return false;

        // Packed/Atlas対応：rect ではなく textureRect を使う
        Rect tr = sp.textureRect; // テクスチャ上の実領域（px）

        float halfW = tr.width * 0.5f;
        float halfH = tr.height * 0.5f;

        // 4分割Rect（px） BL / BR / TL / TR
        Rect[] rects =
        {
            new Rect(tr.xMin,          tr.yMin,          halfW, halfH), // BL
            new Rect(tr.xMin + halfW,  tr.yMin,          halfW, halfH), // BR
            new Rect(tr.xMin,          tr.yMin + halfH,  halfW, halfH), // TL
            new Rect(tr.xMin + halfW,  tr.yMin + halfH,  halfW, halfH), // TR
        };

        Vector2 pivot = new Vector2(0.5f, 0.5f);
        float ppu = sp.pixelsPerUnit;

        root = new GameObject($"{src.gameObject.name}_Pieces");
        root.transform.SetParent(src.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        pieces = new Transform[4];
        baseLocalPos = new Vector3[4];

        Vector3 ext = sp.bounds.extents;
        Vector3[] offsets =
        {
            new Vector3(-ext.x * 0.5f, -ext.y * 0.5f, 0f), // BL
            new Vector3( ext.x * 0.5f, -ext.y * 0.5f, 0f), // BR
            new Vector3(-ext.x * 0.5f,  ext.y * 0.5f, 0f), // TL
            new Vector3( ext.x * 0.5f,  ext.y * 0.5f, 0f), // TR
        };

        // Burst側は「LU,RU,LD,RD」順なので、ここで TL,TR,BL,BR に組み替え
        int[] map = { 2, 3, 0, 1 }; // TL,TR,BL,BR

        int sortingLayerID = src.sortingLayerID;
        int sortingOrder = src.sortingOrder;

        for (int i = 0; i < 4; i++)
        {
            int idx = map[i];

            Sprite pieceSprite;
            try
            {
                pieceSprite = Sprite.Create(tex, rects[idx], pivot, ppu, 0, SpriteMeshType.FullRect);
            }
            catch
            {
                if (root != null) Destroy(root);
                root = null;
                return false;
            }

            GameObject go = new GameObject($"Piece_{i}");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = offsets[idx];
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pieceSprite;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder + 1;

            sr.color = src.color;
            sr.sharedMaterial = src.sharedMaterial;
            sr.flipX = src.flipX;
            sr.flipY = src.flipY;

            pieces[i] = go.transform;
            baseLocalPos[i] = go.transform.localPosition;
        }

        return true;
    }

    void KillSelf()
    {
        if (ownerSpawner != null && myInstance != null)
        {
            ownerSpawner.KillSpawned(myInstance, destroyDelay);
            return;
        }

        Destroy(transform.root.gameObject, destroyDelay);
    }

    bool HasBoolParameter(Animator animator, string paramName)
    {
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == paramName)
                return true;
        }
        return false;
    }
}
