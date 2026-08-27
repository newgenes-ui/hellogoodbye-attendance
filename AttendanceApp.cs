using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Text;
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
        private const string RegistryKeyPath = @"Software\AttendanceTracker";
        private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "AttendanceTracker";

        public AttendanceAppContext()
        {
            // 숨겨진 빈 폼을 MainForm으로 지정하여 트레이 앱 프로세스가 즉시 종료되지 않고 상주하도록 보장
            Form hiddenForm = new Form();
            hiddenForm.Opacity = 0;
            hiddenForm.ShowInTaskbar = false;
            hiddenForm.FormBorderStyle = FormBorderStyle.None;
            hiddenForm.Size = new Size(0, 0);
            hiddenForm.WindowState = FormWindowState.Minimized;
            this.MainForm = hiddenForm;

            LoadSettings();

            // 1. 앱 구동 시 즉시 자동으로 "출근" 이벤트 기록
            RecordAttendance("출근");

            // 2. 트레이 아이콘 및 컨텍스트 메뉴 초기화
            try
            {
                trayIcon = new NotifyIcon();
                trayIcon.Icon = SystemIcons.Application; // 윈도우 기본 애플리케이션 아이콘 사용
                trayIcon.Text = "출퇴근 자동 체크기 (Attendance Tracker)";
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
                // 트레이 아이콘 생성 실패 시 (예: 비대화형 세션 0 구동 시) 로그에 남김
                try
                {
                    File.AppendAllText(@"C:\Attendance\tracker_error.log", string.Format("[{0}] 트레이 생성 실패 (비대화형 세션일 수 있음): {1}\r\n", DateTime.Now, ex.Message));
                }
                catch {}
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
                    }
                    else
                    {
                        employeeName = Environment.UserName; // 지정하지 않았을 때의 기본값
                    }
                }
            }
            catch
            {
                employeeName = Environment.UserName;
            }
        }

        public void RecordAttendance(string action)
        {
            try
            {
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logFilePath = Path.Combine(logDirectory, "attendance_log.csv");
                bool isNew = !File.Exists(logFilePath);

                // UTF-8 BOM 인코딩을 명시하여 Microsoft Excel에서 한글 깨짐 없이 바로 열리도록 설정
                using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                {
                    if (isNew)
                    {
                        writer.WriteLine("DateTime,Action,EmployeeName,ComputerName");
                    }
                    string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    writer.WriteLine(string.Format("{0},{1},{2},{3}", timeStr, action, employeeName, Environment.MachineName));
                }
            }
            catch (Exception ex)
            {
                // 로컬 기록 오류 발생 시 비상용 텍스트 파일 저장 시도
                try
                {
                    File.AppendAllText(@"C:\Attendance\tracker_error.log", string.Format("[{0}] 기록 실패: {1}\r\n", DateTime.Now, ex.Message));
                }
                catch {}
            }
        }

        private void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            // 컴퓨터 종료 또는 로그아웃 시 "퇴근" 처리 실행
            RecordAttendance("퇴근");
        }

        private void ShowSettingsForm(object sender, EventArgs e)
        {
            // 설정 창 띄우기
            FormSettings form = new FormSettings(logDirectory, employeeName, IsStartupEnabled(), this);
            form.ShowDialog();
        }

        public void UpdateSettings(string newDir, string newName, bool enableStartup)
        {
            this.logDirectory = newDir;
            this.employeeName = newName;

            try
            {
                // Registry에 설정 저장
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    key.SetValue("SaveDirectory", newDir);
                    key.SetValue("EmployeeName", newName);
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
            // 사용자가 수동으로 앱을 우클릭하여 종료할 때
            if (trayIcon != null) { trayIcon.Visible = false; }
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
        private DataGridView dgvLogs;
        private AttendanceAppContext context;
        private string logDirectory;

        public FormSettings(string currentDir, string currentName, bool startupEnabled, AttendanceAppContext ctx)
        {
            this.context = ctx;
            this.logDirectory = currentDir;

            // Form 기본 디자인 속성 설정
            this.Text = "출퇴근 자동 체크기 설정";
            this.Size = new Size(520, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 컨트롤들 배치
            Label lblName = new Label() { Text = "사원 이름:", Location = new Point(20, 20), Size = new Size(90, 20) };
            txtName = new TextBox() { Text = currentName, Location = new Point(120, 18), Size = new Size(180, 20) };

            Label lblDir = new Label() { Text = "기록 저장 폴더:", Location = new Point(20, 50), Size = new Size(90, 20) };
            txtDir = new TextBox() { Text = currentDir, Location = new Point(120, 48), Size = new Size(250, 20) };

            Button btnBrowse = new Button() { Text = "찾아보기...", Location = new Point(380, 46), Size = new Size(90, 23) };
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

            chkStartup = new CheckBox() { Text = "윈도우 켤 때 자동으로 앱 실행 (출퇴근 감지)", Checked = startupEnabled, Location = new Point(120, 80), Size = new Size(300, 20) };

            Button btnSave = new Button() { Text = "설정 저장", Location = new Point(120, 110), Size = new Size(100, 30) };
            btnSave.Click += (s, e) =>
            {
                context.UpdateSettings(txtDir.Text, txtName.Text, chkStartup.Checked);
                this.logDirectory = txtDir.Text;
                LoadLogGrid();
                MessageBox.Show("설정이 정상적으로 저장되었습니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // 로그 미리보기 테이블 디자인
            Label lblLog = new Label() { Text = "최근 출퇴근 기록 리스트 (최근 100건):", Location = new Point(20, 160), Size = new Size(300, 20) };
            dgvLogs = new DataGridView()
            {
                Location = new Point(20, 185),
                Size = new Size(460, 220),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D
            };

            this.Controls.AddRange(new Control[] { lblName, txtName, lblDir, txtDir, btnBrowse, chkStartup, btnSave, lblLog, dgvLogs });

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
                    // UTF-8로 저장된 CSV 한 줄씩 읽기
                    string[] lines = File.ReadAllLines(logFilePath, Encoding.UTF8);
                    int count = 0;
                    
                    // 역순(최신 기록 우선)으로 그리드에 행 추가
                    for (int i = lines.Length - 1; i >= 1; i--)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;
                        string[] parts = lines[i].Split(',');
                        if (parts.Length >= 4)
                        {
                            dgvLogs.Rows.Add(parts[0], parts[1], parts[2], parts[3]);
                            count++;
                            if (count >= 100) break; // 최대 100건 표시 제한
                        }
                    }
                }
                catch {}
            }
        }
    }
}
