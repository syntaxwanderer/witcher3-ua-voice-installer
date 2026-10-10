using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

public class MainForm : Form
{
    TextBox path = new TextBox(), log = new TextBox();
    Button browse = new Button(), install = new Button(), remove = new Button();
    ProgressBar bar = new ProgressBar();
    string here = AppDomain.CurrentDomain.BaseDirectory;

    public MainForm()
    {
        Text = "Українська озвучка The Witcher 3 Remastered — v1.5";
        ClientSize = new Size(640, 444); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f);
        Label t = new Label(); t.Text = "Фанатська українська озвучка (синтез ШІ). Неофіційна робота, не схвалена CD PROJEKT RED.\r\nУкраїнська з\u2019явиться окремим пунктом меню озвучення «Українська (ШІ)» (замість бразильської).\r\nПотрібні: The Witcher 3 Remastered (5.0) і польський пакет озвучення (польська не змінюється). Гра має бути закрита.";
        t.SetBounds(14, 6, 612, 58);
        Label l = new Label(); l.Text = "Тека гри:"; l.SetBounds(14, 76, 80, 22);
        path.SetBounds(94, 74, 432, 24);
        browse.Text = "Обрати…"; browse.SetBounds(534, 72, 92, 28);
        install.Text = "Встановити"; install.SetBounds(14, 110, 150, 34);
        remove.Text = "Видалити мод"; remove.SetBounds(172, 110, 150, 34);
        bar.SetBounds(334, 118, 292, 18); bar.Style = ProgressBarStyle.Marquee; bar.Visible = false;
        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical;
        log.SetBounds(14, 154, 612, 276); log.BackColor = SystemColors.Window;
        Controls.AddRange(new Control[] { t, l, path, browse, install, remove, bar, log });
        path.Text = Job.FindGame() ?? "";
        Write(path.Text.Length > 0 ? "Знайдено гру: " + path.Text : "Гру не знайдено автоматично — вкажіть теку гри (де лежать bin і content).");
        browse.Click += delegate
        {
            FolderBrowserDialog d = new FolderBrowserDialog(); d.Description = "Тека The Witcher 3 (де лежать bin і content)";
            if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.SelectedPath;
        };
        install.Click += delegate { Run(false); };
        remove.Click += delegate { Run(true); };
    }

    void Write(string s)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Write), s); return; }
        log.AppendText(s + "\r\n");
    }

    void Run(bool uninstall)
    {
        install.Enabled = remove.Enabled = browse.Enabled = path.Enabled = false; bar.Visible = true;
        Job j = new Job(); j.Here = here; j.Game = path.Text.Trim().Trim('"'); j.Log = Write;
        Thread th = new Thread(delegate ()
        {
            string err;
            try { err = uninstall ? j.Uninstall() : j.Install(); }
            catch (UnauthorizedAccessException) { err = "немає прав на запис у теку гри. Закрийте інсталятор і запустіть його правою кнопкою → «Запустити від імені адміністратора»."; }
            catch (Exception ex) { err = ex.Message; }
            BeginInvoke(new Action(delegate
            {
                bar.Visible = false; install.Enabled = remove.Enabled = browse.Enabled = path.Enabled = true;
                if (err != null) { Write("ПОМИЛКА: " + err); MessageBox.Show(this, err, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                else if (uninstall) { Write("Готово. Мод видалено, гра повернута до оригіналу."); MessageBox.Show(this, "Мод видалено, гра повернута до оригіналу.", "Готово"); }
                else { Write("Готово! Озвучення вже перемкнено на українську. Якщо ні — у грі: Параметри → Мова → Озвучення: «Українська (ШІ)», Текст: українська."); MessageBox.Show(this, "Встановлено!\r\nУ грі: Параметри → Мова → Озвучення: «Українська (ШІ)», Текст: українська.", "Готово"); }
            }));
        });
        th.IsBackground = true; th.Start();
    }

    [STAThread]
    public static int Main(string[] args)
    {
        // Режим без вікна для перевірок: --cli --game <тека> [--uninstall] [--test] [--docs <тека налаштувань>]
        if (Array.IndexOf(args, "--cli") >= 0)
        {
            Job j = new Job(); j.Here = AppDomain.CurrentDomain.BaseDirectory;
            int gi = Array.IndexOf(args, "--game");
            j.Game = gi >= 0 ? args[gi + 1] : Job.FindGame();
            j.Log = delegate (string s) { Console.WriteLine(s); };
            j.SkipRunCheck = Array.IndexOf(args, "--test") >= 0;
            int di = Array.IndexOf(args, "--docs");
            if (di >= 0) j.DocsDir = args[di + 1];
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            string err = Array.IndexOf(args, "--uninstall") >= 0 ? j.Uninstall() : j.Install();
            Console.WriteLine(err == null ? "OK" : "ERROR: " + err);
            return err == null ? 0 : 1;
        }
        Application.EnableVisualStyles();
        Application.Run(new MainForm());
        return 0;
    }
}
