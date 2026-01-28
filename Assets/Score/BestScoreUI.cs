using UnityEngine;
using TMPro;

/*
/// タイトル画面用：BEST 1 / 2 / 3 表示
/// ・各TMP_Textを個別にON/OFFする
*/
public class BestScoreUI : MonoBehaviour
{
    public TMP_Text best1Text;
    public TMP_Text best2Text;
    public TMP_Text best3Text;

    public string prefix = "BEST ";
    public string empty = "-";

    /*
    /// 表示更新（数値反映）
    */
    public void Refresh()
    {
        BestScoreTop3.Get(out int b1, out int b2, out int b3);

        Set(best1Text, 1, b1);
        Set(best2Text, 2, b2);
        Set(best3Text, 3, b3);
    }

    void Set(TMP_Text t, int rank, int score)
    {
        if (t == null) return;

        if (score > 0) t.text = $"{prefix}{rank}: {score}";
        else t.text = $"{prefix}{rank}: {empty}";
    }

    /*
    /// タイトル画面：表示ON
    */
    public void Show()
    {
        SetActive(best1Text, true);
        SetActive(best2Text, true);
        SetActive(best3Text, true);
        Refresh();
    }

    /*
    /// タイトル以外：非表示
    */
    public void Hide()
    {
        SetActive(best1Text, false);
        SetActive(best2Text, false);
        SetActive(best3Text, false);
    }

    void SetActive(TMP_Text t, bool active)
    {
        if (t != null)
            t.gameObject.SetActive(active);
    }
}
