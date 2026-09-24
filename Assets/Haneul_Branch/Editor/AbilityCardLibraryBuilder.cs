using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 프로젝트의 AbilityCard를 전부 긁어 Resources의 카드 목록 에셋에 적어 둔다.
//
// 빌드에서는 AssetDatabase를 못 쓰므로, 카드를 추가하거나 지운 뒤에는 이걸 한 번 눌러야
// 특성 선택 화면(TraitLevelUp)이 새 카드를 뽑는다.
public static class AbilityCardLibraryBuilder
{
    private const string AssetPath = "Assets/Haneul_Branch/Resources/Ability/AbilityCardLibrary.asset";

    [MenuItem("Harpe/능력/카드 목록 갱신")]
    public static void Rebuild()
    {
        var library = AssetDatabase.LoadAssetAtPath<AbilityCardLibrary>(AssetPath);
        if (library == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));
            library = ScriptableObject.CreateInstance<AbilityCardLibrary>();
            AssetDatabase.CreateAsset(library, AssetPath);
        }

        var cards = new List<AbilityCard>();
        foreach (string guid in AssetDatabase.FindAssets("t:AbilityCard"))
        {
            var card = AssetDatabase.LoadAssetAtPath<AbilityCard>(AssetDatabase.GUIDToAssetPath(guid));
            if (card != null) cards.Add(card);
        }
        cards.Sort(delegate(AbilityCard a, AbilityCard b)
        {
            int r = a.Rarity.CompareTo(b.Rarity);
            return r != 0 ? r : string.Compare(a.name, b.name, System.StringComparison.Ordinal);
        });

        Undo.RecordObject(library, "카드 목록 갱신");
        library.SetCards(cards);
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log("[AbilityCardLibrary] 카드 " + cards.Count + "장 기록", library);
    }
}
