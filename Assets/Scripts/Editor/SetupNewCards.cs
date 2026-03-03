using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using DonGame2D.Logic;
using UnityEditor.U2D.Sprites;

namespace DonGame2D.Editor
{
    public class SetupNewCards : EditorWindow
    {
        [MenuItem("Tools/DonGame/Slice and Setup New Cards V3 (Grid)")]
        public static void Setup()
        {
            // 1. A.png (2x2)
            SliceGrid("Assets/Cards/A.png", 2, 2, new string[] {
                "Card_Spades_1", "Card_Hearts_1", // Top Row
                "Card_Clubs_1", "Card_Diamonds_1"  // Bottom Row
            });

            string[] suitsStr = { "Club", "Diamond", "Heart", "Spade" };

            // 2. 2-10 (3x3)
            foreach (var suit in suitsStr)
            {
                string path = $"Assets/Cards/{suit}/{suit}_2-10.png";
                if (File.Exists(path))
                {
                    List<string> names = new List<string>();
                    for (int rank = 2; rank <= 10; rank++)
                    {
                        names.Add($"Card_{suit}s_{rank}");
                    }
                    SliceGrid(path, 3, 3, names.ToArray());
                }
            }

            // 3. J-Q-K (様々なレイアウト)
            // Club: TL=J, TR=Q, BL=A(無視), BR=K -> 2x2
            SliceGrid("Assets/Cards/Club/Club_J-Q-K.png", 2, 2, new string[] {
                "Card_Clubs_11", "Card_Clubs_12", 
                "Ignore", "Card_Clubs_13"
            });
            
            // Diamond: TL=J, TR=Q, BL=K -> 2x2 (BR無し)
            SliceGrid("Assets/Cards/Diamond/Diamond_J-Q-K.png", 2, 2, new string[] {
                "Card_Diamonds_11", "Card_Diamonds_12", 
                "Card_Diamonds_13", "Ignore"
            });
            
            // Heart: TL=J, TR=K, 間の下=Q (V字) -> 2x2のTop=J,K, Bot=中心Q（1枚なのでBotLeftにマッピングして調整するか、2x2でBotMidはどうなる？）
            // 指定: "ハートのJQKは左上にJ、右上にK、JとKの間の下側にQがあります" -> おそらく 2列 x 2行 のグリッドで、下の行は中央にあるかもしれないが、SliceGridで2x2として切るか、Autoで切るほうが良い。
            // とりあえずAutoを使うか、2x2で切るか。2x2で切って大きく余白を取れば表示はされる。
            // BotL/BotRにまたがっている場合は Grid(2,2) では切断される可能性があるため、安全策として 1x2 (上行, 下行) の2枚で切り、上行を2列に分けるなど...いや、ここはAutoSliceが効くことを祈りつつ、効かなければGrid(2,2)で試すフォールバックにする。
            // Heartは特別なレイアウト: JとKが上、Qが下。Grid 2x2で Bot: Q, ignore
            SliceHeartJQK("Assets/Cards/Heart/Heart_J-Q-K.png");

            // Spade: J,K,Q横並び -> 3x1
            SliceGrid("Assets/Cards/Spade/Spade_J-Q-K.png", 3, 1, new string[] {
                "Card_Spades_11", "Card_Spades_13", "Card_Spades_12"
            });

            // Update Database
            UpdateDatabase();
        }

        private static void SliceGrid(string path, int colCount, int rowCount, string[] expectedNames)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool settingsChanged = false;
            if (!importer.isReadable) { importer.isReadable = true; settingsChanged = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; settingsChanged = true; }
            if (importer.spriteImportMode != SpriteImportMode.Multiple) { importer.spriteImportMode = SpriteImportMode.Multiple; settingsChanged = true; }
            
            if (settingsChanged) importer.SaveAndReimport();

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;

            float cellW = (float)tex.width / colCount;
            float cellH = (float)tex.height / rowCount;

            List<SpriteMetaData> metaDataList = new List<SpriteMetaData>();
            int nameIdx = 0;

            // Unity origin (0,0) is bottom-left. We iterate rows top-to-bottom.
            for (int r = rowCount - 1; r >= 0; r--)
            {
                for (int c = 0; c < colCount; c++)
                {
                    if (nameIdx >= expectedNames.Length) break;
                    string spriteName = expectedNames[nameIdx++];
                    
                    if (spriteName != "Ignore")
                    {
                        Rect rect = new Rect(c * cellW, r * cellH, cellW, cellH);
                        Rect trimmedRect = GetTrimmedRect(tex, rect);

                        SpriteMetaData meta = new SpriteMetaData
                        {
                            alignment = 0,
                            name = spriteName,
                            rect = trimmedRect,
                            pivot = new Vector2(0.5f, 0.5f)
                        };
                        metaDataList.Add(meta);
                    }
                }
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider != null)
            {
                dataProvider.InitSpriteEditorDataProvider();
                var existingRects = dataProvider.GetSpriteRects();
                var spriteRects = new List<SpriteRect>();
                foreach (var meta in metaDataList)
                {
                    GUID guid = GUID.Generate();
                    if (existingRects != null)
                    {
                        foreach (var ex in existingRects)
                        {
                            if (ex.name == meta.name)
                            {
                                guid = ex.spriteID;
                                break;
                            }
                        }
                    }

                    spriteRects.Add(new SpriteRect
                    {
                        name = meta.name,
                        rect = meta.rect,
                        alignment = SpriteAlignment.Center,
                        pivot = meta.pivot,
                        spriteID = guid
                    });
                }
                dataProvider.SetSpriteRects(spriteRects.ToArray());
                dataProvider.Apply();
            }

            importer.SaveAndReimport();
            Debug.Log($"Sliced Grid {path} into {metaDataList.Count} sprites.");
        }

        private static void SliceHeartJQK(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            if (!importer.isReadable) importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.SaveAndReimport();

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;

            // ユーザー曰く: "ハートのJQKは左上にJ、右上にK、JとKの間の下側にQがあります。V字に配置"
            // 上行を2つに分割 (幅半分ずつ、高さは上の半分)
            // 下行は中央に1つ (幅半分か全幅、高さは下の半分)
            
            List<SpriteMetaData> metaDataList = new List<SpriteMetaData>();
            
            float cellW = tex.width / 2f;
            float cellH = tex.height / 2f;

            // TL = J
            metaDataList.Add(new SpriteMetaData {
                name = "Card_Hearts_11", alignment = 0, pivot = new Vector2(0.5f, 0.5f),
                rect = GetTrimmedRect(tex, new Rect(0, cellH, cellW, cellH))
            });

            // TR = K
            metaDataList.Add(new SpriteMetaData {
                name = "Card_Hearts_13", alignment = 0, pivot = new Vector2(0.5f, 0.5f),
                rect = GetTrimmedRect(tex, new Rect(cellW, cellH, cellW, cellH))
            });

            // Bot Mid = Q (幅は1/2にして中央に配置。x=25%〜75%)
            metaDataList.Add(new SpriteMetaData {
                name = "Card_Hearts_12", alignment = 0, pivot = new Vector2(0.5f, 0.5f),
                rect = GetTrimmedRect(tex, new Rect(tex.width * 0.25f, 0, cellW, cellH))
            });

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider != null)
            {
                dataProvider.InitSpriteEditorDataProvider();
                var existingRects = dataProvider.GetSpriteRects();
                var spriteRects = new List<SpriteRect>();
                foreach (var meta in metaDataList)
                {
                    GUID guid = GUID.Generate();
                    if (existingRects != null)
                    {
                        foreach (var ex in existingRects)
                        {
                            if (ex.name == meta.name)
                            {
                                guid = ex.spriteID;
                                break;
                            }
                        }
                    }

                    spriteRects.Add(new SpriteRect
                    {
                        name = meta.name,
                        rect = meta.rect,
                        alignment = SpriteAlignment.Center,
                        pivot = meta.pivot,
                        spriteID = guid
                    });
                }
                dataProvider.SetSpriteRects(spriteRects.ToArray());
                dataProvider.Apply();
            }

            importer.SaveAndReimport();
            Debug.Log($"Sliced Heart JQK into {metaDataList.Count} sprites.");
        }

        private static void UpdateDatabase()
        {
            CardDatabase database = AssetDatabase.LoadAssetAtPath<CardDatabase>("Assets/Resources/CardDatabase.asset");
            if (database == null) return;

            database.cardEntries.Clear();

            string backPath = "Assets/2D Cards Game Art Pack/Sprites/Standard 52 Cards/Standard Rounded Cards/Card Back/cardBackBlue.png";
            database.cardBackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(backPath);

            string[] suitsStr = { "Clubs", "Diamonds", "Hearts", "Spades" };
            DonGame2D.Models.Suit[] suits = { DonGame2D.Models.Suit.Clubs, DonGame2D.Models.Suit.Diamonds, DonGame2D.Models.Suit.Hearts, DonGame2D.Models.Suit.Spades };

            for (int i = 0; i < suitsStr.Length; i++)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    string spriteName = $"Card_{suitsStr[i]}_{rank}";
                    
                    string[] possiblePaths = {
                        "Assets/Cards/A.png",
                        $"Assets/Cards/{suitsStr[i].TrimEnd('s')}/{suitsStr[i].TrimEnd('s')}_2-10.png",
                        $"Assets/Cards/{suitsStr[i].TrimEnd('s')}/{suitsStr[i].TrimEnd('s')}_J-Q-K.png"
                    };

                    Sprite foundSprite = null;
                    foreach (var path in possiblePaths)
                    {
                        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
                        var sprite = assets.OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
                        if (sprite != null)
                        {
                            foundSprite = sprite;
                            break;
                        }
                    }

                    if (foundSprite != null)
                    {
                        database.cardEntries.Add(new CardDatabase.CardSpriteEntry
                        {
                            suit = suits[i],
                            rank = rank,
                            sprite = foundSprite
                        });
                    }
                }
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log($"CardDatabase updated! {database.cardEntries.Count} cards added.");
        }

        private static Rect GetTrimmedRect(Texture2D tex, Rect originalRect)
        {
            int texWidth = tex.width;
            int texHeight = tex.height;
            Color32[] pixels = tex.GetPixels32();

            int xMin = Mathf.RoundToInt(originalRect.x);
            int yMin = Mathf.RoundToInt(originalRect.y);
            int width = Mathf.RoundToInt(originalRect.width);
            int height = Mathf.RoundToInt(originalRect.height);

            int xMax = Mathf.Clamp(xMin + width - 1, 0, texWidth - 1);
            int yMax = Mathf.Clamp(yMin + height - 1, 0, texHeight - 1);
            xMin = Mathf.Clamp(xMin, 0, texWidth - 1);
            yMin = Mathf.Clamp(yMin, 0, texHeight - 1);

            int left = xMax;
            int right = xMin;
            int top = yMax;
            int bottom = yMin;

            bool found = false;

            for (int y = yMin; y <= yMax; y++)
            {
                int rowOffset = y * texWidth;
                for (int x = xMin; x <= xMax; x++)
                {
                    if (pixels[rowOffset + x].a > 10)
                    {
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < bottom) bottom = y;
                        if (y > top) top = y;
                        found = true;
                    }
                }
            }

            if (!found) return originalRect;

            // 1ピクセルの余白
            left = Mathf.Max(xMin, left - 1);
            bottom = Mathf.Max(yMin, bottom - 1);
            right = Mathf.Min(xMax, right + 1);
            top = Mathf.Min(yMax, top + 1);

            return new Rect(left, bottom, right - left + 1, top - bottom + 1);
        }
    }
}
