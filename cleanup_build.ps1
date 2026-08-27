# cleanup_build.ps1
# 줄바꿈 없는 한 줄 Where-Object 구문을 사용하여 완벽하게 빌드에 성공하게 만드는 스크립트

$hel = [System.Text.Encoding]::UTF8.GetString([byte[]](0xed, 0x97, 0xac, 0xeb, 0xa1, 0x9c))
$good = [System.Text.Encoding]::UTF8.GetString([byte[]](0xea, 0xb5, 0xbf))
$geut = [System.Text.Encoding]::UTF8.GetString([byte[]](0xea, 0xb5, 0xbb))
$gyot = [System.Text.Encoding]::UTF8.GetString([byte[]](0xea, 0xb5, 0xa3))
$gool = [System.Text.Encoding]::UTF8.GetString([byte[]](0xea, 0xb5, 0xa7))

# 1. 락 프로세스 킬 (한 줄로 완벽히 기입)
Get-Process | Where-Object { $PSItem.Name -like "*$hel*" -or $PSItem.Name -like "*$good*" -or $PSItem.Name -like "*$geut*" -or $PSItem.Name -like "*$gyot*" -or $PSItem.Name -like "*$gool*" -or $PSItem.Name -like "*Attendance*" } | Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 1

$currentDir = Get-Location

# 2. 오타 파일들 정리
$targets = @("헬로굿바이.exe", "헬로굣바이.exe", "헬로굻바이.exe", "헬로긋바이.exe")
foreach ($t in $targets) {
    $p = Join-Path $currentDir $t
    if (Test-Path $p) {
        Remove-Item -Path $p -Force -ErrorAction SilentlyContinue
    }
}

# 3. 바탕화면 단축아이콘 정리
$desktopPath = [System.IO.Path]::Combine($env:USERPROFILE, "Desktop")
Get-ChildItem $desktopPath | Where-Object { $PSItem.Name -like "*$hel*" -or $PSItem.Name -like "*Attendance*" } | Remove-Item -Force -ErrorAction SilentlyContinue

# 4. 빌드 구동
$buildScript = Join-Path $currentDir "build.ps1"
if (Test-Path $buildScript) {
    "" | powershell.exe -ExecutionPolicy Bypass -File $buildScript
}
