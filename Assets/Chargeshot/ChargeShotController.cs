using UnityEngine;
using UnityEngine.InputSystem;

/*
     チャージショット専用コントローラ

     【やりたいこと】
     ・押している間チャージする
     ・離した瞬間に弾を発射する
     ・チャージが成立していたら「通常より2倍大きい弾」を出す（localScaleで変更）
     ・入力は旧Input（UnityEngine.Input）を使わず、新InputSystem（UnityEngine.InputSystem）だけを使う
       → InvalidOperationException を避ける

     【設計方針】
     ・通常ショット（BulletController）とは別スクリプトで完結させる
     ・弾生成は PoolManager を使う（既存の仕組みに乗る）
     ・transformアクセスはキャッシュして読みやすくする
     ・弾の当たり判定を大きくしたい場合は「弾側」に半径を持たせるのが確実
       （EnemyControllerは Bullet.GetHitRadius() を参照する想定）
*/
public sealed class ChargeShotController : MonoBehaviour
{
    //================
    // Reference
    //================

    [Header("References")]
    [Tooltip("弾を取得するプール")]
    [SerializeField] private PoolManager Pool;

    [Tooltip("弾の出現位置の基準（プレイヤーTransform）")]
    [SerializeField] private Transform Player;

    //================
    // Charge Settings
    //================

    [Header("Charge Settings")]
    [Tooltip("この秒数以上押してから離すとチャージショットになる")]
    [SerializeField] private float ChargeTime = 0.6f;

    [Tooltip("チャージ成立時の弾スケール倍率（2倍なら2）")]
    [SerializeField] private float ChargedScaleMultiplier = 2.0f;

    [Tooltip("チャージ中に弾を撃てないようにする（通常ショットと併用時の事故防止）")]
    [SerializeField] private bool BlockOtherFireWhileCharging = true;

    //================
    // Bullet Hit Radius (Optional)
    //================

    /*
         ここは「弾の当たり判定が大きくならない」対策用オプション

         ・弾の見た目だけscaleで大きくしても、当たり判定が別管理だと大きくならない
         ・今回のプロジェクトは EnemyController が「距離判定」で当たり判定しているので
           弾側の当たり半径(Bullet.GetHitRadius())が増えない限り、判定は増えない

         → そのため、チャージ弾を撃つ時に Bullet.SetHitRadiusMultiplier を呼んで
           弾側の当たり半径を広げる運用にできるようにしている

         ※ Bullet にこのAPIが無い場合は、下の SetChargedBulletHitRadius() の呼び出しをコメントアウトしてOK
    */
    [Header("Charged Bullet Radius (Optional)")]
    [Tooltip("チャージ成立時に弾の当たり半径も拡大する（見た目と一致させたい場合）")]
    [SerializeField] private bool ExpandBulletHitRadius = true;

    [Tooltip("当たり半径倍率（見た目2倍なら半径も2倍が分かりやすい）")]
    [SerializeField] private float ChargedHitRadiusMultiplier = 2.0f;

    //================
    // Audio
    //================

    [Header("Audio")]
    [Tooltip("未設定ならこのコンポーネントから取得する")]
    [SerializeField] private AudioSource SeSource;

    [Tooltip("チャージ完了時に鳴らすSE（任意）")]
    [SerializeField] private AudioClip ChargeReadySe;

    [Range(0f, 1f)]
    [SerializeField] private float ChargeReadySeVolume = 1.0f;

    [Tooltip("離して発射した瞬間に鳴らすSE（任意）")]
    [SerializeField] private AudioClip ReleaseFireSe;

    [Range(0f, 1f)]
    [SerializeField] private float ReleaseFireSeVolume = 1.0f;

    //================
    // Input
    //================

    /*
         InputSystem_Actions を使う（旧Inputは使わない）
         ・Input.Player.Attack をチャージ入力として使う想定
         ・Attackが通常ショットでも使われている場合は、別Action（Chargeなど）を割り当て推奨
    */
    private InputSystem_Actions Input;

    //================
    // State
    //================

    private bool IsCharging = false;
    private float ChargeTimer = 0f;
    private bool HasPlayedReadySe = false;

    //================
    // Cache
    //================

    private Transform PlayerTransform;

    //================
    // Unity Event
    //================

    private void Awake()
    {
        /*
             InputSystem_Actions を生成
             Enable/Disable は OnEnable/OnDisable で管理する
        */
        Input = new InputSystem_Actions();

        /*
             Transform参照をキャッシュ
        */
        PlayerTransform = Player;

        /*
             AudioSourceの自動取得（任意）
        */
        if (SeSource == null) SeSource = GetComponent<AudioSource>();
        if (SeSource != null) SeSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        /*
             Player入力を有効化する
        */
        if (Input != null) Input.Player.Enable();
    }

    private void OnDisable()
    {
        /*
             Player入力を無効化する
        */
        if (Input != null) Input.Player.Disable();

        /*
             無効化されたらチャージ状態もリセットして事故を防ぐ
        */
        ResetChargeState();
    }

    private void Start()
    {
        /*
             参照チェック（未設定はエラー）
        */
        if (Pool == null) Debug.LogError("[ChargeShotController] Pool が未設定です（PoolManager を設定してください）");
        if (Player == null) Debug.LogError("[ChargeShotController] Player が未設定です（Transform を設定してください）");

        PlayerTransform = Player;

        if (ChargeTime < 0f) ChargeTime = 0f;
        if (ChargedScaleMultiplier < 0.01f) ChargedScaleMultiplier = 0.01f;
        if (ChargedHitRadiusMultiplier < 0.01f) ChargedHitRadiusMultiplier = 0.01f;
    }

    private void Update()
    {
        /*
             参照が無いなら動けない（Startでエラーは出ている）
        */
        if (Pool == null) return;
        if (PlayerTransform == null) return;
        if (Input == null) return;

        //================
        // Input State
        //================

        /*
             Attackボタンの押下状態を取得する（新InputSystem）

             ・IsPressed() は「押している間true」
             ・離した瞬間は IsPressed が false に切り替わる
        */
        bool IsPressed = Input.Player.Attack.IsPressed();

        //================
        // Charge Start
        //================

        /*
             押された瞬間にチャージ開始する

             ・押している間にチャージが進む
             ・まだチャージしていない状態で押されたら開始
        */
        if (!IsCharging && IsPressed)
        {
            BeginCharge();
        }

        //================
        // Charging
        //================

        /*
             チャージ中はタイマーを進める
             チャージ成立時にSEを鳴らす（任意）
        */
        if (IsCharging && IsPressed)
        {
            ChargeTimer += Time.deltaTime;

            // チャージ成立（初回だけ）
            if (!HasPlayedReadySe && ChargeTimer >= ChargeTime)
            {
                HasPlayedReadySe = true;
                PlayOneShot(ChargeReadySe, ChargeReadySeVolume);
            }

            /*
                 チャージ中に他の発射をブロックしたい場合は、
                 ここで通常ショット側の ControlEnabled を落とすなどの設計にできる
                 （本クラス単体では他スクリプト参照しない方針なので、ここでは何もしない）
            */
        }

        //================
        // Release
        //================

        /*
             離した瞬間（押していない & チャージ中）に発射する
        */
        if (IsCharging && !IsPressed)
        {
            ReleaseAndFire();
        }
    }

    //================
    // Charge Flow
    //================

    private void BeginCharge()
    {
        /*
             チャージ開始

             ・タイマー初期化
             ・状態をチャージ中へ
        */
        IsCharging = true;
        ChargeTimer = 0f;
        HasPlayedReadySe = false;
    }

    private void ReleaseAndFire()
    {
        /*
             ボタンを離した瞬間の処理

             ・チャージ時間を満たしていれば「チャージ弾」
             ・満たしていなければ「通常弾（サイズそのまま）」
        */
        bool IsCharged = ChargeTimer >= ChargeTime;

        FireBullet(IsCharged);

        ResetChargeState();
    }

    private void ResetChargeState()
    {
        /*
             状態を初期化する

             ・途中でDisableされた時もここを通す
        */
        IsCharging = false;
        ChargeTimer = 0f;
        HasPlayedReadySe = false;
    }

    //================
    // Fire
    //================

    private void FireBullet(bool IsCharged)
    {
        /*
             弾を発射する

             【順番】
             1) プールから弾を取得
             2) 位置をプレイヤー位置に合わせる
             3) チャージなら scale を 2倍にする
             4) 必要なら当たり半径も拡大する（弾側APIがある前提）
             5) 発射SE（任意）
        */
        Vector3 SpawnPos = PlayerTransform.position;
        Quaternion SpawnRot = Quaternion.identity;

        GameObject BulletInstance = Pool.GetGameObject(SpawnPos, SpawnRot);
        if (BulletInstance == null)
        {
            Debug.LogError("[ChargeShotController] Pool から弾が取得できませんでした（GetGameObject が null を返しました）");
            return;
        }

        Transform BulletTransform = BulletInstance.transform;

        //================
        // Scale Reset / Apply
        //================

        /*
             プールは「前回の状態が残る」可能性があるので、
             まず scale を必ず通常値に戻してから、チャージなら倍率をかける

             ※通常値を「Prefabのscale」と一致させたい場合は、
               Bullet側に初期scaleを保持する仕組みを作るのが理想。
               ここでは簡単実装として Vector3.one を基準にする。
        */
        BulletTransform.localScale = Vector3.one;

        if (IsCharged)
        {
            BulletTransform.localScale = Vector3.one * ChargedScaleMultiplier;
        }

        //================
        // Hit Radius (Optional)
        //================

        if (IsCharged && ExpandBulletHitRadius)
        {
            SetChargedBulletHitRadius(BulletInstance, ChargedHitRadiusMultiplier);
        }
        else
        {
            // 通常弾として半径倍率を戻したい場合（APIがあるならここで戻す）
            SetChargedBulletHitRadius(BulletInstance, 1.0f);
        }

        //================
        // SE
        //================

        /*
             離した瞬間の発射SE（任意）
        */
        PlayOneShot(ReleaseFireSe, ReleaseFireSeVolume);
    }

    //================
    // Bullet Hit Radius Helper (Optional)
    //================

    private void SetChargedBulletHitRadius(GameObject BulletInstance, float Multiplier)
    {
        /*
             弾側に「当たり半径倍率」を渡す補助

             重要：
             ・EnemyControllerが Bullet.GetHitRadius() を見て当たり判定している前提
             ・Bullet側に下記のようなAPIが必要

               public float GetHitRadius() { ... }
               public void SetHitRadiusMultiplier(float mul) { ... }

             Bulletにまだ無いなら、まず Bullet を拡張してから使う。
             ひとまず安全に null チェックして「無いなら何もしない」設計にしている。
        */
        if (BulletInstance == null) return;

        Bullet Bt = BulletInstance.GetComponent<Bullet>();
        if (Bt == null) return;

        // Bullet側に SetHitRadiusMultiplier がある想定
        // 無い場合はここでコンパイルエラーになるので、Bulletに追加するか、この行をコメントアウトする
        Bt.SetHitRadiusMultiplier(Multiplier);
    }

    //================
    // Audio Helper
    //================

    private void PlayOneShot(AudioClip Clip, float Volume)
    {
        /*
             ワンショットSE再生

             ・Clipが無ければ何もしない
             ・SeSourceが無いなら鳴らせないので何もしない（エラーを増やさない）
        */
        if (Clip == null) return;
        if (SeSource == null) return;

        SeSource.PlayOneShot(Clip, Mathf.Clamp01(Volume));
    }
}
