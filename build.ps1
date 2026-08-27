# build.ps1
# C# 코드를 실행 가능 어플리케이션(.exe)으로 즉석 빌드해 주는 컴파일 스크립트

$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    # 32비트 윈도우 OS 대비 예외 처리
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "C# 컴파일러(csc.exe)를 찾을 수 없습니다. 윈도우 환경에 .NET Framework가 존재하지 않거나 경로가 맞지 않습니다."
    Read-Host "계속하려면 엔터를 누르세요..."
    exit 1
}

$outputExe = Join-Path $PSScriptRoot "AttendanceTracker.exe"
$sourceFile = Join-Path $PSScriptRoot "AttendanceApp.cs"

Write-Host "C# 컴파일러를 사용하여 데스크톱 어플(AttendanceTracker.exe) 빌드를 시작합니다..." -ForegroundColor Cyan

# 컴파일 실행:
# -target:winexe는 콘솔창이 뜨지 않고 GUI로만 구동되게 만드는 옵션입니다.
# -win32icon은 실행 파일(.exe)에 적용할 리소스 아이콘 파일을 지정합니다.
$iconPath = Join-Path $PSScriptRoot "app.ico"
& $cscPath /target:winexe /win32icon:"$iconPath" /out:"$outputExe" "$sourceFile"

if ($LASTEXITCODE -eq 0 -and (Test-Path $outputExe)) {
    Write-Host "`n[성공] 어플리케이션 빌드가 성공적으로 완료되었습니다!" -ForegroundColor Green
    Write-Host "- 실행 파일 생성 경로: $outputExe" -ForegroundColor Gray
    Write-Host "이제 생성된 AttendanceTracker.exe 파일을 더블클릭하여 바로 사용하실 수 있습니다." -ForegroundColor Green
} else {
    Write-Error "컴파일 빌드 중 에러가 발생했습니다. C# 문법 오류 또는 파일 경로를 다시 점검하십시오."
}

Read-Host "`n종료하려면 엔터를 누르세요..."
