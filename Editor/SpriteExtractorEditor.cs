using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.U2D.Sprites; // Sprite Data Provider (Otomatik dilimleme) için gerekli

public class SpriteExtractorEditor
{
    // ===================================================================================
    // 1. ÖZELLİK: DÜZ TEXTURE DOSYALARINI SPRITE'A ÇEVİRME (NO FILTER & READ/WRITE)
    // ===================================================================================

    [MenuItem("Assets/Sprite İşlemleri/1- Seçili Resimleri Sprite'a Çevir (Make Setting For Sprites)")]
    public static void ConvertToSprite()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);

        if (selectedObjects.Length == 0) return;
        
        int count = 0;
        foreach (Object obj in selectedObjects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 0;
        importer.isReadable = true;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }
        }

        Debug.Log("İşlem Tamam! " + count + " adet dosya başarıyla Sprite formatına dönüştürüldü.");
    }

    [MenuItem("Assets/Sprite İşlemleri/1- Seçili Resimleri Sprite'a Çevir (Make Setting For Sprites)", true)]
    private static bool ConvertToSpriteValidation()
    {
        return Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets).Length > 0;
    }
    // ===================================================================================
    // 2. ÖZELLİK: ZATEN DİLİMLENMİŞ SPRİTELARI AYRI PNG OLARAK ÇIKARTMA
    // ===================================================================================
    [MenuItem("Assets/Sprite İşlemleri/2- Dilimlenmiş Spriteleri Çıkart (Extract Multiple Sprites)")]
    public static void ExtractSpritesMenu()
    {
        Texture2D selectedTexture = Selection.activeObject as Texture2D;

        if (selectedTexture == null)
        {
            Debug.LogWarning("Lütfen önce dilimlenmiş bir Sprite (Multiple) dosyası seçin.");
            return;
        }

        ExtractSpritesFromTexture(selectedTexture);
    }

    [MenuItem("Assets/Sprite İşlemleri/2- Dilimlenmiş Spriteleri Çıkart (Extract Multiple Sprites)", true)]
    private static bool ExtractSpritesValidation()
    {
        return Selection.activeObject is Texture2D;
    }




    // ===================================================================================
    // 3. ÖZELLİK: TEK TIKLA OTOMATİK DİLİMLE VE PARÇALARI ÇIKART (YENİ)
    // ===================================================================================
    [MenuItem("Assets/Sprite İşlemleri/3- Otomatik Dilimle ve Ayır (Auto-Slice & Extract)")]
    public static void AutoSliceAndExtract()
    {
        Texture2D selectedTexture = Selection.activeObject as Texture2D;
        if (selectedTexture == null) return;

        string path = AssetDatabase.GetAssetPath(selectedTexture);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null) return;

        // 1. Resmi Sprite (Multiple) yap, Point Filter ve Read/Write aç
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 0;
        importer.isReadable = true;

        // Asset'i güncelle
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // 2. Unity'nin Sprite Editor'deki otomatik dilimleme algoritmasını çalıştır
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Rect[] rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(tex, 4, 0);

        // 3. Bulunan dilimleri Sprite Editor meta verisine (Data Provider) yaz
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dataProvider = factory.GetSpriteEditorDataProviderFromObject(tex);
        dataProvider.InitSpriteEditorDataProvider();

        var spriteRects = new List<SpriteRect>();
        for (int i = 0; i < rects.Length; i++)
        {
            spriteRects.Add(new SpriteRect
            {
                name = tex.name + "_" + i,
                rect = rects[i],
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                spriteID = GUID.Generate()
            });
        }

        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();

        // Dilimleme işlemini kaydet ve dosyayı yeniden import et
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // 4. Şimdi bu yeni dilimleri PNG olarak dışarı çıkart (Yardımcı fonksiyonu kullanarak)
        ExtractSpritesFromTexture(tex);
    }

    [MenuItem("Assets/Sprite İşlemleri/3- Otomatik Dilimle ve Ayır (Auto-Slice & Extract)", true)]
    private static bool AutoSliceAndExtractValidation()
    {
        return Selection.activeObject is Texture2D;
    }


    // ===================================================================================
    // YARDIMCI FONKSİYON: Dışarı Çıkartma Mantığı (2 ve 3 numaralı özellikler kullanıyor)
    // ===================================================================================
    private static void ExtractSpritesFromTexture(Texture2D selectedTexture)
    {
        string path = AssetDatabase.GetAssetPath(selectedTexture);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

        string folderPath = Path.GetDirectoryName(path);

        // Not: Eğer çıkan dosyalar çok fazla olup klasörü karıştırırsa diye, alt klasör oluşturmak istersen
        // alttaki satırı aktif edip, üsttekini silebilirsin:
        // string folderPath = Path.GetDirectoryName(path) + "/" + selectedTexture.name + "_Parcalar";

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        int count = 0;
        List<string> generatedFilePaths = new List<string>();

        foreach (Object asset in allAssets)
        {
            if (asset is Sprite)
            {
                Sprite sprite = (Sprite)asset;

                Texture2D newTex = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
                Color[] pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, (int)sprite.rect.width, (int)sprite.rect.height);

                newTex.SetPixels(pixels);
                newTex.Apply();

                byte[] bytes = newTex.EncodeToPNG();
                string newFilePath = folderPath + "/" + sprite.name + ".png";
                File.WriteAllBytes(newFilePath, bytes);

                generatedFilePaths.Add(newFilePath);
                count++;
            }
        }

        AssetDatabase.Refresh();

        foreach (string filePath in generatedFilePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.anisoLevel = 0;
        importer.isReadable = true;

                AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            }
        }

        Debug.Log("İşlem Tamam! Toplam " + count + " adet alt sprite çıkartıldı ve ayarları (No Filter vb.) uygulandı.");
    }
}



