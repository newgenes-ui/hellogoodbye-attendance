# attendance_monitor.ps1
# 이 스크립트는 로그인 시 출근을 체크하고, 백그라운드에서 대기하다가 컴퓨터 종료/로그아웃 시 퇴근을 체크합니다.

# 현재 스크립트가 있는 경로에서 check_attendance.ps1 경로 계산
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $scriptDir) { $scriptDir = "C:\Attendance" }
$attendanceScript = Join-Path $scriptDir "check_attendance.ps1"

# 1. 즉시 출근 기록 실행
if (Test-Path $attendanceScript) {
    & $attendanceScript -Action "ON"
} else {
    Write-Error "출퇴근 기록 스크립트를 찾을 수 없습니다: $attendanceScript"
}

# 모니터 자체의 동작 확인을 위한 로그 디렉토리 및 파일 설정
$monitorLog = "C:\Attendance\monitor_status.log"
try {
    if (-not (Test-Path "C:\Attendance")) {
        New-Item -ItemType Directory -Path "C:\Attendance" -Force | Out-Null
    }
    "[{0}] Monitor started. ON action recorded." -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss") | Out-File $monitorLog -Append -Encoding UTF8
} catch {}

# 2. 시스템 종료 및 로그오프 이벤트 감지 등록 (.NET Event Handler 사용)
Add-Type -AssemblyName System.Windows.Forms

$sessionEndingHandler = {
    param($sender, $e)
    
    # 퇴근 기록 실행
    if (Test-Path $attendanceScript) {
        & $attendanceScript -Action "OFF"
    }
    
    try {
        "[{0}] Session ending detected. Reason: {1}. OFF action recorded." -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $e.Reason | Out-File $monitorLog -Append -Encoding UTF8
    } catch {}
}

# 이벤트 핸들러 등록
[Microsoft.Win32.SystemEvents]::add_SessionEnding($sessionEndingHandler)

# 스크립트가 종료되지 않고 백그라운드에서 계속 이벤트를 수신하도록 대기 루프 실행
try {
    [System.Windows.Forms.Application]::Run()
}
finally {
    # 예기치 않게 루프를 빠져나갈 때 이벤트 핸들러 해제
    [Microsoft.Win32.SystemEvents]::remove_SessionEnding($sessionEndingHandler)
}
