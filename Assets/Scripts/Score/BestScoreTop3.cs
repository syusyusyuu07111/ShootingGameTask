using UnityEngine;

/// <summary>
/// ベストスコアを上位3つだけ保持する
/// ルール：
/// ・空きがあるなら追加して並べ替え
/// ・3つ埋まってたら「最小（3位）」と比較
///    - 新スコア <= 最小  → 破棄
///    - 新スコア >  最小  → 最小を捨てて追加 → 並べ替え
/// </summary>
public static class BestScoreTop3
{
    const string KEY1 = "BEST_SCORE_1";
    const string KEY2 = "BEST_SCORE_2";
    const string KEY3 = "BEST_SCORE_3";

    /// <summary>
    /// 現在のTop3を取得（存在しない分は0）
    /// 常に b1 >= b2 >= b3 の順になるようにして返す
    /// </summary>
    public static void Get(out int b1, out int b2, out int b3)
    {
        b1 = PlayerPrefs.GetInt(KEY1, 0);
        b2 = PlayerPrefs.GetInt(KEY2, 0);
        b3 = PlayerPrefs.GetInt(KEY3, 0);
        SortDesc(ref b1, ref b2, ref b3);
    }

    /// <summary>
    /// スコアを登録する（Top3に入る可能性がある時だけ保存される）
    /// 戻り値：Top3に入ったかどうか
    /// </summary>
    public static bool TryRegister(int score)
    {
        if (score <= 0) return false;

        int b1, b2, b3;
        Get(out b1, out b2, out b3);

        // まだ3枠埋まっていない（0が空き扱い）ならそのまま入れて並べ替え
        if (b3 == 0)
        {
            InsertAndSort(score, ref b1, ref b2, ref b3);
            Save(b1, b2, b3);
            return true;
        }

        // 4つ目以降：最小（3位）と比較
        // 小さい（または同点）なら破棄
        if (score <= b3)
            return false;

        // 大きいなら3位を捨てて追加→並べ替え
        b3 = score;
        SortDesc(ref b1, ref b2, ref b3);
        Save(b1, b2, b3);
        return true;
    }

    static void InsertAndSort(int score, ref int b1, ref int b2, ref int b3)
    {
        // 空き(0)がある前提で、空いてるところに入れてから並べ替え
        if (b1 == 0) b1 = score;
        else if (b2 == 0) b2 = score;
        else b3 = score;

        SortDesc(ref b1, ref b2, ref b3);
    }

    static void Save(int b1, int b2, int b3)
    {
        PlayerPrefs.SetInt(KEY1, b1);
        PlayerPrefs.SetInt(KEY2, b2);
        PlayerPrefs.SetInt(KEY3, b3);
        PlayerPrefs.Save();
    }

    // 3つの数値を降順に並べ替え（b1 >= b2 >= b3）
    static void SortDesc(ref int a, ref int b, ref int c)
    {
        if (a < b) Swap(ref a, ref b);
        if (b < c) Swap(ref b, ref c);
        if (a < b) Swap(ref a, ref b);
    }

    static void Swap(ref int x, ref int y)
    {
        int tmp = x;
        x = y;
        y = tmp;
    }

    /// <summary>
    /// デバッグ用：ベストスコアを全消し
    /// </summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY1);
        PlayerPrefs.DeleteKey(KEY2);
        PlayerPrefs.DeleteKey(KEY3);
        PlayerPrefs.Save();
    }
}
