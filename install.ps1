# install.ps1
# 사원 출퇴근 자동 체크 시스템 설치 스크립트 (관리자 권한으로 실행 필요)

# 1. 관리자 권한 확인 및 재실행
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "작업을 수행하기 위해 관리자 권한이 필요합니다." -ForegroundColor Yellow
    Write-Host "관리자 권한으로 스크립트를 재실행하는 중..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# 2. 고정 경로 폴더 설정
$targetDir = "C:\Attendance"
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    Write-Host "기록 저장용 디렉토리 생성 완료: $targetDir" -ForegroundColor Cyan
}

# 3. 모든 사용자가 기록할 수 있도록 디렉토리 권한(ACL) 수정 (일반 사용자 권한으로도 CSV 작성 가능하도록)
try {
    $acl = Get-Acl $targetDir
    # 로컬 컴퓨터의 "Users" 그룹에 수정(Modify) 권한 부여 (하위 파일 및 폴더 상속 설정)
    $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        "Users", 
        "Modify", 
        "ContainerInherit,ObjectInherit", 
        "None", 
        "Allow"
    )
    $acl.SetAccessRule($accessRule)
    Set-Acl $targetDir $acl
    Write-Host "디렉토리 권한 설정 완료 (모든 사용자 쓰기 권한 허용)" -ForegroundColor Cyan
} catch {
    Write-Warning "디렉토리 권한을 설정하는 데 실패했습니다. 일반 사용자가 기록을 작성할 수 없게 될 수 있습니다: $_"
}

# 4. 스크립트 파일들을 고정 경로로 복사
$filesToCopy = @("check_attendance.ps1", "attendance_monitor.ps1")
foreach ($file in $filesToCopy) {
    $srcPath = Join-Path $PSScriptRoot $file
    if (Test-Path $srcPath) {
        Copy-Item -Path $srcPath -Destination $targetDir -Force
        Write-Host "파일 복사 완료: $file -> $targetDir" -ForegroundColor Green
    } else {
        Write-Error "필요한 소스 파일을 찾을 수 없습니다: $srcPath"
        Read-Host "종료하려면 엔터를 누르세요..."
        exit
    }
}

# 5. Windows 작업 스케줄러 등록
$taskName = "Attendance_Check_System"
$monitorScript = Join-Path $targetDir "attendance_monitor.ps1"

# 기존에 동일한 이름의 작업이 있으면 제거
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false | Out-Null
    Write-Host "기존에 등록된 작업 스케줄러 일정을 제거했습니다." -ForegroundColor Cyan
}

# 작업 정보 구성
# -WindowStyle Hidden 옵션으로 powershell 창이 깜빡이지 않고 백그라운드에서 실행되도록 설정
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -File `"$monitorScript`""
$trigger = New-ScheduledTaskTrigger -AtLogOn
# 노트북 등에서 배터리 모드일 때도 실행되도록 설정하고 시간 제한 무제한 적용
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Days 365)
# 모든 사용자가 로그인할 때 인터랙티브 세션 내에서 구동되도록 설정
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\INTERACTIVE" -LogonType Interactive

# 작업 등록 실행
try {
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
    Write-Host "`n[성공] 사원 출퇴근 자동 체크 시스템이 작업 스케줄러에 정상 등록되었습니다!" -ForegroundColor Green
    Write-Host "- 스크립트 경로: $targetDir" -ForegroundColor Gray
    Write-Host "- 출퇴근 기록 CSV 파일: C:\Attendance\attendance_log.csv" -ForegroundColor Gray
    Write-Host "- 작동 방식: PC 로그인 시 자동으로 출근(ON) 기록, 컴퓨터 종료/로그아웃 시 퇴근(OFF) 기록" -ForegroundColor Gray
} catch {
    Write-Error "작업 스케줄러 등록 중 오류가 발생했습니다: $_"
}

Read-Host "`n설치가 완료되었습니다. 엔터를 누르면 창이 닫힙니다..."
