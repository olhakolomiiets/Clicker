using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BatchSliceSpriteAtlases
{
    const int CellW = 400;
    const int CellH = 600;
    const int SpacingX = 2;
    const int SpacingY = 2;
    const int OffsetX = 8;
    const int OffsetY = 8;
    const float PixelsPerUnit = 100f;

    [MenuItem("Tools/Sprites/Batch Slice Selected Atlases")]
    static void SliceSelectedAtlases()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                continue;

            int columns = 0;
            for (int x = OffsetX; x + CellW <= tex.width - OffsetX; x += CellW + SpacingX)
                columns++;

            int rows = 0;
            for (int y = OffsetY; y + CellH <= tex.height - OffsetY; y += CellH + SpacingY)
                rows++;

            List<SpriteMetaData> sprites = new List<SpriteMetaData>();
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int x = OffsetX + col * (CellW + SpacingX);

                    // Unity rect считает Y снизу, поэтому переворачиваем,
                    // чтобы порядок был сверху-вниз
                    int y = tex.height - OffsetY - CellH - row * (CellH + SpacingY);

                    SpriteMetaData meta = new SpriteMetaData
                    {
                        name = tex.name + "_" + index.ToString("00"),
                        rect = new Rect(x, y, CellW, CellH),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };

                    sprites.Add(meta);
                    index++;
                }
            }

            importer.spritesheet = sprites.ToArray();

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"Sliced {path}: {sprites.Count} sprites ({columns}x{rows})");
        }

        AssetDatabase.Refresh();
        Debug.Log("Batch slicing finished.");
    }
}