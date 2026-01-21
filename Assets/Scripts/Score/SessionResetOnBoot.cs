using UnityEngine;

/// <summary>
/// アプリ起動時に「保存データ（PlayerPrefs）」を消して、毎回リセットする
/// ・ビルドを閉じたら次回起動でBESTも消える
/// </summary>
public class SessionResetOnBoot : MonoBehaviour
{
    [Tooltip("起動時にBESTスコア(Top3)をリセットする")]
    public bool resetBestTop3 = true;

    void Awake()
    {
        if (resetBestTop3)
        {
            // BestScoreTop3 が持ってるキーだけ消す（安全）
            BestScoreTop3.Clear();
        }

        // もし「他のPlayerPrefsも全部消したい」なら下を使う
        // PlayerPrefs.DeleteAll();
        // PlayerPrefs.Save();
    }
}
