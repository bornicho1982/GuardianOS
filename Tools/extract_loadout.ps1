$toolDir = "E:\GuardianOS\Tools\ColladaGenerator"
$toolExe = ".\DestinyColladaGenerator-v1-7-15.exe"
$outDir = "Output\DestinyModel0"
$cacheBase = "E:\GuardianOS\Tools\TextureCache"
$hashes = @("2752429099", "2748506263", "1172384181", "4112577340", "1109145282")

Set-Location -Path $toolDir

foreach ($hash in $hashes) {
    Write-Host "----------------------------------------"
    Write-Host "Processing Item: $hash"
    
    # 1. Clean Output
    if (Test-Path $outDir) {
        Remove-Item $outDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    # 2. Run Tool
    # Note: Using & operator to run command
    Write-Host "Running ColladaGenerator..."
    try {
        & $toolExe -g $hash
    } catch {
        Write-Host "Error running tool: $_"
        continue
    }
    
    # 3. Check and Move
    if (Test-Path $outDir) {
        $destPath = Join-Path $cacheBase $hash "DestinyModel0"
        if (-not (Test-Path $destPath)) {
            New-Item -ItemType Directory -Force -Path $destPath | Out-Null
        }
        
        Write-Host "Caching textures to: $destPath"
        Copy-Item "$outDir\*" -Destination $destPath -Recurse -Force
        
        $files = Get-ChildItem -Path $destPath -Filter "*.png" -Recurse
        Write-Host "Success! Cached $($files.Count) textures."
    } else {
        Write-Host "ERROR: No output generated for $hash"
    }
    
    Start-Sleep -Seconds 1
}

Write-Host "----------------------------------------"
Write-Host "Batch Extraction Complete."
