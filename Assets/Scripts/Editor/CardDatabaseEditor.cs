using UnityEngine;
using UnityEditor;
using System.IO;
using DonGame2D.Logic;
using DonGame2D.Models;

namespace DonGame2D.Editor
{
    public static class CardDatabaseEditor
    {
        private const string DATABASE_PATH = "Assets/Resources/CardDatabase.asset";
        private const string OLD_SPRITES_BASE_PATH = "Assets/2D Cards Game Art Pack/Sprites/Standard 52 Cards/Standard Rounded Cards/";
        private const string NEW_SPRITES_BASE_PATH = "Assets/Cards/";

        [MenuItem("Tools/DonGame/Setup Card Database V2")]
        public static void SetupDatabase()
        {
            // データベースアセットの取得、または作成
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>(DATABASE_PATH);
            bool isNew = false;
            
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(database, DATABASE_PATH);
                isNew = true;
                Debug.Log("Created new CardDatabase asset.");
            }

            database.cardEntries.Clear();

            // カードの裏面の取得
            string backPath = Path.Combine(OLD_SPRITES_BASE_PATH, "Card Back", "cardBackBlue.png");
            database.cardBackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backPath);
            if (database.cardBackSprite == null)
            {
                Debug.LogWarning($"Card back sprite not found at {backPath}");
            }

            // カード表面の取得
            string[] suitFolders = { "Club", "Diamond", "Heart", "Spade" };
            Suit[] suitEnums = { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades };

            for (int i = 0; i < suitFolders.Length; i++)
            {
                string suitFolder = suitFolders[i];
                Suit suitEnum = suitEnums[i];

                for (int rank = 1; rank <= 13; rank++)
                {
                    string fileName = $"torannpu-illust{rank}.png";
                    string filePath = Path.Combine(NEW_SPRITES_BASE_PATH, suitFolder, fileName);

                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
                    if (sprite != null)
                    {
                        var entry = new CardDatabase.CardSpriteEntry
                        {
                            suit = suitEnum,
                            rank = rank,
                            sprite = sprite
                        };
                        database.cardEntries.Add(entry);
                    }
                    else
                    {
                        Debug.LogWarning($"Card sprite not found at {filePath}");
                    }
                }
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>CardDatabase Setup Complete. Found {database.cardEntries.Count} cards and {(database.cardBackSprite ? 1 : 0)} card back.</color>");
            EditorGUIUtility.PingObject(database);
            Selection.activeObject = database;
        }
    }
}
