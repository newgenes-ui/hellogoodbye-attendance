using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Collections.Generic;
using Microsoft.Win32;

namespace AttendanceTracker
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new AttendanceAppContext());
            }
            catch (Exception ex)
            {
                try
                {
                    if (!Directory.Exists(@"C:\Attendance"))
                    {
                        Directory.CreateDirectory(@"C:\Attendance");
                    }
                    File.WriteAllText(@"C:\Attendance\startup_error.txt", ex.ToString());
                }
                catch {}
            }
        }
    }

    public class AttendanceAppContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private string logDirectory = @"C:\Attendance";
        private string employeeName = "";

        // 구글 폼 연동 설정 키
        public string googleFormId = "";
        public string entryNameId = "";
        public string entryActionId = "";
        public string entryComputerId = "";

        private const string RegistryKeyPath = @"Software\HelloGoodbyeAttendance";
        private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "HelloGoodbyeAttendance";

        public AttendanceAppContext()
        {
            LoadSettings();

            // 1. 앱 구동 시 즉시 자동으로 "출근" 이벤트 기록 (로컬 및 구글)
            RecordAttendance("출근");

            // 2. 트레이 아이콘 및 컨텍스트 메뉴 초기화
            try
            {
                trayIcon = new NotifyIcon();
                
                // 실행 파일 자체에 심어진 아이콘 리소스를 추출하여 트레이 아이콘으로 사용
                try
                {
                    trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch
                {
                    trayIcon.Icon = SystemIcons.Application;
                }
                
                trayIcon.Text = "헬로굿바이 (출퇴근 자동 체크기)";
                trayIcon.Visible = true;

                ContextMenu trayMenu = new ContextMenu();
                trayMenu.MenuItems.Add("설정 및 로그 보기", ShowSettingsForm);
                trayMenu.MenuItems.Add("-");
                trayMenu.MenuItems.Add("종료", Exit);
                trayIcon.ContextMenu = trayMenu;

                // 아이콘 더블클릭 시 설정창 열기
                trayIcon.DoubleClick += ShowSettingsForm;
            }
            catch (Exception ex)
            {
                LogToFile(string.Format("트레이 생성 실패: {0}", ex.Message));
            }

            // 3. 컴퓨터 종료 및 로그아웃 이벤트 구독 (.NET SystemEvents)
            SystemEvents.SessionEnding += OnSessionEnding;
        }

        private void LoadSettings()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        logDirectory = key.GetValue("SaveDirectory", @"C:\Attendance").ToString();
                        employeeName = key.GetValue("EmployeeName", Environment.UserName).ToString();
                        
                        // 구글 폼 셋업 로드
                        googleFormId = key.GetValue("GoogleFormId", "").ToString();
                        entryNameId = key.GetValue("EntryNameId", "").ToString();
                        entryActionId = key.GetValue("EntryActionId", "").ToString();
                        entryComputerId = key.GetValue("EntryComputerId", "").ToString();
                    }
                    else
                    {
                        employeeName = Environment.UserName;
                    }
                }
            }
            catch
            {
                employeeName = Environment.UserName;
            }
        }

        public void RecordAttendance(string action, bool isShutdown = false)
        {
            string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 1. 로컬 CSV 파일에 쓰기 (오프라인 상태 대비 백업)
            RecordToLocalCsv(timeStr, action);

            // 2. 구글 스프레드시트(구글 폼)에 실시간 전송 (인터넷 연결 상태인 경우)
            if (!string.IsNullOrEmpty(googleFormId) &&
                !string.IsNullOrEmpty(entryNameId) &&
                !string.IsNullOrEmpty(entryActionId) &&
                !string.IsNullOrEmpty(entryComputerId))
            {
                if (isShutdown)
                {
                    // 컴퓨터 종료 시에는 동기적으로 대기하여 전송 완료 보장
                    try
                    {
                        SendToGoogleFormAsync(action).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        LogToFile(string.Format("종료 중 구글 전송 대기 실패: {0}", ex.Message));
                    }
                }
                else
                {
                    // 평소(출근 등)에는 비동기 백그라운드로 처리하여 UI 프리징 방지
                    System.Threading.Tasks.Task.Run(() => SendToGoogleFormAsync(action));
                }
            }
        }

        private void RecordToLocalCsv(string timeStr, string action)
        {
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logFilePath = Path.Combine(logDirectory, "attendance_log.csv");
                bool isNew = !File.Exists(logFilePath);
                string today = DateTime.Now.ToString("yyyy-MM-dd");

                List<string> lines = new List<string>();
                if (!isNew)
                {
                    lines.AddRange(File.ReadAllLines(logFilePath, Encoding.UTF8));
                }

                bool hasTodayOn = false;
                int todayOffIndex = -1;

                // 오늘 날짜의 출퇴근 기록이 이미 있는지 조회
                for (int i = 1; i < lines.Count; i++)
                {
                    string[] parts = lines[i].Split(',');
                    if (parts.Length >= 3)
                    {
                        string logDate = parts[0].Split(' ')[0]; // yyyy-MM-dd 추출
                        string logAction = parts[1];
                        string logUser = parts[2];

                        if (logDate == today && logUser == employeeName)
                        {
                            if (logAction == "출근")
                            {
                                hasTodayOn = true;
                            }
                            else if (logAction == "퇴근")
                            {
                                todayOffIndex = i;
                            }
                        }
                    }
                }

                string newRow = string.Format("{0},{1},{2},{3}", timeStr, action, employeeName, Environment.MachineName);

                if (action == "출근")
                {
                    // 오늘 이미 출근이 기록되어 있다면 추가하지 않고 무시 (하루 1회만 보장)
                    if (hasTodayOn)
                    {
                        return;
                    }
                    else
                    {
                        if (isNew)
                        {
                            lines.Add("DateTime,Action,EmployeeName,ComputerName");
                        }
                        lines.Add(newRow);
                    }
                }
                else if (action == "퇴근")
                {
                    if (todayOffIndex != -1)
                    {
                        // 오늘 이미 퇴근 기록이 있다면 최신 퇴근 시각으로 그 행을 업데이트 (하루 1회 보장)
                        lines[todayOffIndex] = newRow;
                    }
                    else
                    {
                        if (isNew)
                        {
                            lines.Add("DateTime,Action,EmployeeName,ComputerName");
                        }
                        lines.Add(newRow);
                    }
                }

                File.WriteAllLines(logFilePath, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogToFile(string.Format("로컬 저장 실패: {0}", ex.Message));
            }
        }

        private async System.Threading.Tasks.Task SendToGoogleFormAsync(string action)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 네트워크 타임아웃 8초 설정 (윈도우 프리징 현상 방지)
                    client.Timeout = TimeSpan.FromSeconds(8);

                    var postData = new Dictionary<string, string>();
                    postData.Add(entryNameId, employeeName);
                    postData.Add(entryActionId, action);
                    postData.Add(entryComputerId, Environment.MachineName);

                    var content = new FormUrlEncodedContent(postData);
                    string url = string.Format("https://docs.google.com/forms/d/e/{0}/formResponse", googleFormId);

                    HttpResponseMessage response = await client.PostAsync(url, content).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        LogToFile("구글 스프레드시트 실시간 전송 완료");
                    }
                    else
                    {
                        LogToFile(string.Format("구글 전송 실패 (서버 응답 오류): {0}", response.StatusCode));
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile(string.Format("구글 전송 실패 (오프라인 또는 차단): {0}", ex.Message));
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                if (!Directory.Exists(@"C:\Attendance"))
                {
                    Directory.CreateDirectory(@"C:\Attendance");
                }
                File.AppendAllText(@"C:\Attendance\tracker_error.log", string.Format("[{0}] {1}\r\n", DateTime.Now, message));
            }
            catch {}
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            // 컴퓨터 종료/로그아웃 시 "퇴근" 처리
            RecordAttendance("퇴근", true);
        }

        private void ShowSettingsForm(object sender, EventArgs e)
        {
            FormSettings form = new FormSettings(logDirectory, employeeName, IsStartupEnabled(), this);
            form.ShowDialog();
        }

        public void UpdateSettings(string newDir, string newName, bool enableStartup, string gFormId, string eNameId, string eActionId, string eCompId)
        {
            this.logDirectory = newDir;
            this.employeeName = newName;
            this.googleFormId = gFormId;
            this.entryNameId = eNameId;
            this.entryActionId = eActionId;
            this.entryComputerId = eCompId;

            try
            {
                // Registry에 설정 저장
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    key.SetValue("SaveDirectory", newDir);
                    key.SetValue("EmployeeName", newName);
                    key.SetValue("GoogleFormId", gFormId);
                    key.SetValue("EntryNameId", eNameId);
                    key.SetValue("EntryActionId", eActionId);
                    key.SetValue("EntryComputerId", eCompId);
                }

                // 윈도우 시작프로그램(Run) 설정 저장/삭제
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, true))
                {
                    if (enableStartup)
                    {
                        key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("설정을 저장하는 과정에서 오류가 발생했습니다: {0}", ex.Message), "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKeyPath))
                {
                    if (key != null)
                    {
                        return key.GetValue(AppName) != null;
                    }
                }
            }
            catch {}
            return false;
        }

        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            SystemEvents.SessionEnding -= OnSessionEnding;
            if (this.MainForm != null) { this.MainForm.Close(); }
            Application.Exit();
        }
    }

    public class FormSettings : Form
    {
        private TextBox txtDir;
        private TextBox txtName;
        private CheckBox chkStartup;
        
        // 구글 폼 입력 컨트롤
        private TextBox txtFormId;
        private TextBox txtEntryName;
        private TextBox txtEntryAction;
        private TextBox txtEntryComputer;

        private DataGridView dgvLogs;
        private AttendanceAppContext context;
        private string logDirectory;

        public FormSettings(string currentDir, string currentName, bool startupEnabled, AttendanceAppContext ctx)
        {
            this.context = ctx;
            this.logDirectory = currentDir;

            // Form 디자인 설정 (구글 연동을 추가하기 위해 창의 세로 크기를 650으로 확장)
            this.Text = "헬로굿바이 설정";
            this.Size = new Size(540, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 1. 기본 설정 그룹박스
            GroupBox gbBasic = new GroupBox() { Text = "기본 설정", Location = new Point(15, 10), Size = new Size(495, 135) };
            
            Label lblName = new Label() { Text = "사원 이름:", Location = new Point(15, 25), Size = new Size(95, 20) };
            txtName = new TextBox() { Text = currentName, Location = new Point(120, 22), Size = new Size(180, 20) };

            Label lblDir = new Label() { Text = "기록 저장 폴더:", Location = new Point(15, 55), Size = new Size(95, 20) };
            txtDir = new TextBox() { Text = currentDir, Location = new Point(120, 53), Size = new Size(240, 20) };

            Button btnBrowse = new Button() { Text = "찾아보기...", Location = new Point(370, 51), Size = new Size(110, 23) };
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.SelectedPath = txtDir.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtDir.Text = fbd.SelectedPath;
                    }
                }
            };

            chkStartup = new CheckBox() { Text = "윈도우 켤 때 자동으로 앱 실행 (출퇴근 감지)", Checked = startupEnabled, Location = new Point(120, 85), Size = new Size(350, 20) };
            gbBasic.Controls.AddRange(new Control[] { lblName, txtName, lblDir, txtDir, btnBrowse, chkStartup });

            // 2. 구글 스프레드시트 실시간 연동 그룹박스 (선택사항)
            GroupBox gbGoogle = new GroupBox() { Text = "구글 스프레드시트 실시간 연동 (선택사항)", Location = new Point(15, 155), Size = new Size(495, 155) };
            
            Label lblFormId = new Label() { Text = "구글 폼 ID:", Location = new Point(15, 25), Size = new Size(95, 20) };
            txtFormId = new TextBox() { Text = context.googleFormId, Location = new Point(120, 22), Size = new Size(355, 20) };

            Label lblEntryName = new Label() { Text = "사원명 Entry:", Location = new Point(15, 55), Size = new Size(95, 20) };
            txtEntryName = new TextBox() { Text = context.entryNameId, Location = new Point(120, 52), Size = new Size(110, 20) };

            Label lblEntryAction = new Label() { Text = "구분 Entry:", Location = new Point(245, 55), Size = new Size(90, 20) };
            txtEntryAction = new TextBox() { Text = context.entryActionId, Location = new Point(345, 52), Size = new Size(130, 20) };

            Label lblEntryComputer = new Label() { Text = "PC명 Entry:", Location = new Point(15, 85), Size = new Size(95, 20) };
            txtEntryComputer = new TextBox() { Text = context.entryComputerId, Location = new Point(120, 82), Size = new Size(110, 20) };

            Label lblHint = new Label() { 
                Text = "* 구글 폼 ID와 각 entry ID를 모두 채워두면 실시간 웹 전송이 작동합니다.", 
                ForeColor = Color.DimGray, 
                Location = new Point(15, 115), 
                Size = new Size(460, 30) 
            };

            gbGoogle.Controls.AddRange(new Control[] { lblFormId, txtFormId, lblEntryName, txtEntryName, lblEntryAction, txtEntryAction, lblEntryComputer, txtEntryComputer, lblHint });

            // 3. 설정 저장 버튼
            Button btnSave = new Button() { Text = "설정 저장", Location = new Point(210, 320), Size = new Size(120, 35), Font = new Font(this.Font, FontStyle.Bold) };
            btnSave.Click += (s, e) =>
            {
                context.UpdateSettings(txtDir.Text, txtName.Text, chkStartup.Checked, txtFormId.Text, txtEntryName.Text, txtEntryAction.Text, txtEntryComputer.Text);
                this.logDirectory = txtDir.Text;
                LoadLogGrid();
                MessageBox.Show("설정이 정상적으로 저장되었습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 4. 로그 리스트 영역
            Label lblLog = new Label() { Text = "최근 출퇴근 기록 리스트 (최근 100건):", Location = new Point(15, 370), Size = new Size(300, 20) };
            dgvLogs = new DataGridView()
            {
                Location = new Point(15, 395),
                Size = new Size(495, 210),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D
            };

            this.Controls.AddRange(new Control[] { gbBasic, gbGoogle, btnSave, lblLog, dgvLogs });

            LoadLogGrid();
        }

        private void LoadLogGrid()
        {
            dgvLogs.Columns.Clear();
            dgvLogs.Columns.Add("DateTime", "일시 (DateTime)");
            dgvLogs.Columns.Add("Action", "구분 (Action)");
            dgvLogs.Columns.Add("EmployeeName", "사원명 (User)");
            dgvLogs.Columns.Add("ComputerName", "PC명 (Host)");

            string logFilePath = Path.Combine(logDirectory, "attendance_log.csv");
            if (File.Exists(logFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(logFilePath, Encoding.UTF8);
                    int count = 0;
                    for (int i = lines.Length - 1; i >= 1; i--)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        string[] parts = lines[i].Split(',');
                        if (parts.Length >= 4)
                        {
                            dgvLogs.Rows.Add(parts[0], parts[1], parts[2], parts[3]);
                            count++;
                            if (count >= 100) break;
                        }
                    }
                }
                catch {}
            }
        }
    }
}
