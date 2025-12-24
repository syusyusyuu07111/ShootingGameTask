using UnityEngine;

/// <summary>
/// “G”í’eSE‚ğˆêŒ³ŠÇ—‚·‚éƒNƒ‰ƒX
/// EEnemyController ‚©‚çŒÄ‚Î‚ê‚é
/// E‘½dÄ¶ / ‰¹—Ê / ŠÔˆø‚« / ‹——£Œ¸Š‚ğ‚±‚±‚Å§Œä
/// </summary>
public class EnemyHitSEManager : MonoBehaviour
{
    public static EnemyHitSEManager Instance { get; private set; }

    [Header("Audio")]
    public AudioSource seSource;
    public AudioClip enemyHitSE;

    [Range(0f, 1f)]
    public float volume = 1.0f;

    [Header("Limiter")]
    [Tooltip("‚±‚Ì•b”ˆÈ“à‚Ì˜A‘±Ä¶‚Í–³‹i‘½d–h~j")]
    public float minInterval = 0.05f;

    float lastPlayTime = -999f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (seSource == null)
            seSource = GetComponent<AudioSource>();

        if (seSource == null)
            seSource = gameObject.AddComponent<AudioSource>();

        seSource.playOnAwake = false;
        seSource.loop = false;
        seSource.spatialBlend = 0f; // 2D‰¹
    }

    /// <summary>
    /// “G‚ª”í’e‚µ‚½uŠÔ‚ÉŒÄ‚Ô
    /// </summary>
    public void PlayEnemyHit()
    {
        if (enemyHitSE == null) return;

        // ‘½dÄ¶–h~
        if (Time.time - lastPlayTime < minInterval)
            return;

        lastPlayTime = Time.time;

        seSource.PlayOneShot(enemyHitSE, volume);
    }
}
