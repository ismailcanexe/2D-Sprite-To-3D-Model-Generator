using UnityEngine;
using UnityEditor;
using System.IO;

public class SpriteExtractorEditor
{
    // Unity'de dosyalara sağ tıkladığımızda çıkacak menü seçeneği
    [MenuItem("Assets/Dilimlenmiş Spriteleri Çıkart (PNG)")]
    public static void ExtractSprites()
    {
        // Seçili olan dosyayı al
        Texture2D selectedTexture = Selection.activeObject as Texture2D;

        if (selectedTexture == null)
        {
            Debug.LogWarning("Lütfen önce dilimlenmiş bir Sprite (Multiple) dosyası seçin.");
            return;
        }

        // Texture'ın dosya yolunu bul ve altındaki tüm dilimlenmiş spriteleri çek
        string path = AssetDatabase.GetAssetPath(selectedTexture);
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);

        string folderPath = Path.GetDirectoryName(path) + "/" + selectedTexture.name + "_Ayrilmis";

        // Çıkartılacak resimler için yeni bir klasör oluştur
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        int count = 0;

        foreach (Object asset in allAssets)
        {
            if (asset is Sprite)
            {
                Sprite sprite = (Sprite)asset;

                // Orijinal texture'dan sadece bu sprite'ın olduğu alanı kes
                Texture2D newTex = new Texture2D((int)sprite.rect.width, (int)sprite.rect.height);
                Color[] pixels = sprite.texture.GetPixels((int)sprite.rect.x, (int)sprite.rect.y, (int)sprite.rect.width, (int)sprite.rect.height);

                newTex.SetPixels(pixels);
                newTex.Apply();

                // PNG olarak kaydet
                byte[] bytes = newTex.EncodeToPNG();
                File.WriteAllBytes(folderPath + "/" + sprite.name + ".png", bytes);
                count++;
            }
        }

        // Projeyi yenile ki yeni dosyalar görünür olsun
        AssetDatabase.Refresh();
        Debug.Log("İşlem Tamam! Toplam " + count + " adet alt sprite bağımsız dosya olarak çıkartıldı.");
    }

    // Bu seçeneğin sadece Texture2D dosyalarında görünmesini sağlayan kontrol
    [MenuItem("Assets/Dilimlenmiş Spriteleri Çıkart (PNG)", true)]
    private static bool ExtractSpritesValidation()
    {
        return Selection.activeObject is Texture2D;
    }
}