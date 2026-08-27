# build.ps1
# C# 코드를 실행 가능 어플리케이션으로 즉석 빌드해 주는 컴파일 스크립트 (한글 인코딩 우회 버전)

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "C# 컴파일러(csc.exe)를 찾을 수 없습니다."
    Read-Host "계속하려면 엔터를 누르세요..."
    exit 1
}

# 파워쉘 한글 깨짐 방지를 위해 UTF-8 바이트 배열을 역디코딩하여 "헬로굿바이" 파일명 획득
$appNameKorean = [System.Text.Encoding]::UTF8.GetString([byte[]](0xed, 0x97, 0xac, 0xeb, 0xa1, 0x9c, 0xea, 0xb5, 0xbf, 0xeb, 0xb0, 0x94, 0xec, 0x9d, 0xb4))
$outputExe = Join-Path $PSScriptRoot "$appNameKorean.exe"
$sourceFile = Join-Path $PSScriptRoot "AttendanceApp.cs"

Write-Host "C# 컴파일러를 사용하여 데스크톱 어플($appNameKorean.exe) 빌드를 시작합니다..." -ForegroundColor Cyan

# 컴파일 실행
$iconPath = Join-Path $PSScriptRoot "app.ico"
& $cscPath /target:winexe /win32icon:"$iconPath" /reference:System.Net.Http.dll /out:"$outputExe" "$sourceFile"

if ($LASTEXITCODE -eq 0 -and (Test-Path $outputExe)) {
    Write-Host "`n[성공] 어플리케이션 빌드가 성공적으로 완료되었습니다!" -ForegroundColor Green
    Write-Host "- 실행 파일 생성 경로: $outputExe" -ForegroundColor Gray
    Write-Host "이제 생성된 $appNameKorean.exe 파일을 더블클릭하여 바로 사용하실 수 있습니다." -ForegroundColor Green
} else {
    Write-Error "컴파일 빌드 중 에러가 발생했습니다."
}

Read-Host "`n종료하려면 엔터를 누르세요..."
