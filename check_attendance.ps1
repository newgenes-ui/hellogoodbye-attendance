param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("ON", "OFF")]
    [string]$Action
)

# 기록 저장 디렉토리 및 파일명 설정 (기본: C:\Attendance)
$logDirectory = "C:\Attendance"
$logFile = Join-Path $logDirectory "attendance_log.csv"

try {
    # 디렉토리가 없으면 생성
    if (-not (Test-Path $logDirectory)) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }

    # CSV 파일이 없으면 헤더행 작성
    if (-not (Test-Path $logFile)) {
        # UTF-8 BOM 인코딩으로 작성하여 Excel에서 한글이나 레이아웃이 깨지지 않도록 함
        "DateTime,Action,User,HostName" | Out-File -FilePath $logFile -Encoding UTF8 -Force
    }

    # 현재 기록 정보 생성
    $now = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $user = $env:USERNAME
    $computer = $env:COMPUTERNAME

    # 출퇴근 동작 매핑 (인코딩 깨짐 방지를 위해 UTF-8 바이트 배열을 디코딩하여 대입)
    $actionKorean = if ($Action -eq "ON") { 
        [System.Text.Encoding]::UTF8.GetString([byte[]](0xec, 0xb6, 0x9c, 0xea, 0xb7, 0xbc)) # "출근"
    } else { 
        [System.Text.Encoding]::UTF8.GetString([byte[]](0xed, 0x87, 0xb4, 0xea, 0xb7, 0xbc)) # "퇴근"
    }

    # CSV 행 데이터 생성
    $logLine = "$now,$actionKorean,$user,$computer"

    # 기록 추가
    Add-Content -Path $logFile -Value $logLine -Encoding UTF8
}
catch {
    Write-Error "출퇴근 기록 중 오류 발생: $_"
}
