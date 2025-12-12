# Fix-PortraitLoader.ps1 - 修复 PortraitLoader.cs 文件截断问题

Write-Host "修复 PortraitLoader.cs 文件..." -ForegroundColor Yellow

$filePath = "Source\TheSecondSeat\PersonaGeneration\PortraitLoader.cs"

# 在文件末尾添加缺失的方法
$missingMethods = @"

                // 确保纹理尺寸一致
                int width = Mathf.Min(bottom.width, top.width);
                int height = Mathf.Min(bottom.height, top.height);
                
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color bottomColor = bottom.GetPixel(x, y);
                        Color topColor = top.GetPixel(x, y);
                        
                        // Alpha blending
                        Color blended = Color.Lerp(bottomColor, topColor, topColor.a);
                        result.SetPixel(x, y, blended);
                    }
                }
                
                result.Apply();
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(`$"[PortraitLoader] 合成纹理失败: {ex}");
                return bottom;
            }
        }
        
        /// <summary>
        /// 获取人格文件夹名称
        /// </summary>
        private static string GetPersonaFolderName(NarratorPersonaDef def)
        {
            if (!string.IsNullOrEmpty(def.narratorName))
            {
                // 取第一个单词（如 "Cassandra Classic" → "Cassandra"）
                return def.narratorName.Split(' ')[0].Trim();
            }
            
            string defName = def.defName;
            string[] suffixesToRemove = new[] { "_Default", "_Classic", "_Custom", "_Persona" };
            foreach (var suffix in suffixesToRemove)
            {
                if (defName.EndsWith(suffix))
                {
                    return defName.Substring(0, defName.Length - suffix.Length);
                }
            }
            
            return defName;
        }
    }
}
"@

# 检查文件是否以不完整的方式结尾
$content = Get-Content $filePath -Raw
if ($content -notmatch "}\s*}\s*$") {
    Write-Host "文件不完整，添加缺失的方法..." -ForegroundColor Yellow
    Add-Content -Path $filePath -Value $missingMethods -Encoding UTF8
    Write-Host "? 修复完成！" -ForegroundColor Green
} else {
    Write-Host "? 文件已完整" -ForegroundColor Green
}
