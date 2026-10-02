// Українська озвучка The Witcher 3 Remastered (5.0) — інсталятор. Відкритий код.
// Українська стає окремим пунктом меню озвучення: займає слот бразильської португальської
// (content0\brpc.w3speech), зібраний з ВАШОГО польського файлу. Польська озвучка не змінюється.
// Назва пункту меню: три рядки у вашому content0\ua.w3strings ("Українська (ШІ)"), копія — ua.w3strings.bak_ua5.
// Нічого не завантажує з інтернету. Збирає мод із ВАШИХ файлів гри:
// у пакеті лише наш звук, XOR-різниця ліпсинку і доріжки оповідача.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

public class Job
{
    public string Here, Game;
    public bool SkipRunCheck;   // лише для перевірок на тестовій копії гри
    public Action<string> Log = delegate { };
    Dictionary<string, object> man;
    string dat;
    // власне розширення копій: у тих, хто ставив 0.9, лежать *.bak_ua від гри 4.04
    const string BAK = ".bak_ua5";

    public static bool IsGame(string d)
    {
        return !string.IsNullOrEmpty(d) && Directory.Exists(Path.Combine(d, "content\\content0")) && Directory.Exists(Path.Combine(d, "bin"));
    }

    public static string FindGame()
    {
        List<string> c = new List<string>();
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
            {
                string sp = k == null ? null : k.GetValue("SteamPath") as string;
                if (sp != null)
                {
                    List<string> libs = new List<string>(); libs.Add(sp);
                    string vdf = Path.Combine(sp, "steamapps\\libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                    foreach (string l in libs) c.Add(Path.Combine(l, "steamapps\\common\\The Witcher 3"));
                }
            }
        }
        catch { }
        foreach (string id in new string[] { "1207664663", "1495134320", "1640424747" })
            foreach (string root in new string[] { "SOFTWARE\\WOW6432Node\\GOG.com\\Games\\", "SOFTWARE\\GOG.com\\Games\\" })
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(root + id))
                    {
                        string p = k == null ? null : k.GetValue("path") as string;
                        if (p != null) c.Add(p);
                    }
                }
                catch { }
            }
        foreach (string d in c) if (IsGame(d)) return d;
        return null;
    }

    Dictionary<string, object> Obj(object o) { return (Dictionary<string, object>)o; }
    uint U(object o) { return Convert.ToUInt32(o); }
    long L(object o) { return Convert.ToInt64(o); }

    void Load()
    {
        JavaScriptSerializer js = new JavaScriptSerializer(); js.MaxJsonLength = int.MaxValue;
        man = Obj(js.DeserializeObject(File.ReadAllText(Path.Combine(Here, "manifest.json"))));
        dat = Path.Combine(Here, "ua_voice.dat");
    }

    static bool GameRunning() { return Process.GetProcessesByName("witcher3").Length > 0; }

    const string SLOT = "BR";                 // код мови в налаштуваннях гри
    // Файл озвучення сам несе свій ключ у заголовку, тож польські ключі під бразильською назвою читаються як є.
    // (Переписувати ключі не можна: індекс відсортовано за ключем, гра шукає двійковим пошуком.)
    static readonly Dictionary<uint, string> LABELS = new Dictionary<uint, string> {
        { 1263253, "Українська (ШІ)" }, { 1084969, "Українська (ШІ)" }, { 1228445, "Українське озвучення (ШІ)" } };
    string UaStrings { get { return Path.Combine(Content0, "ua.w3strings"); } }
    string Content0 { get { return Path.Combine(Game, "content\\content0"); } }
    string Bundle { get { return Path.Combine(Content0, "bundles\\movies.bundle"); } }
    string ModDir { get { return Path.Combine(Game, "mods\\modUAVoice"); } }
    // тека налаштувань гри; для перевірок на тестовій копії її можна підмінити (--docs)
    public string DocsDir;
    string Docs { get { return DocsDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "The Witcher 3"); } }

    // повертає null, якщо все добре, інакше текст помилки
    public string Install()
    {
        if (!IsGame(Game)) return "у теці немає гри The Witcher 3: " + Game;
        if (!SkipRunCheck && GameRunning()) return "гра запущена. Закрийте її і спробуйте ще раз.";
        Load();
        Log("перевіряю пакет...");
        if (!File.Exists(dat) || new FileInfo(dat).Length != L(man["dat_size"]) || W3UA.CrcFile(dat) != U(man["dat_crc"]))
            return "файл ua_voice.dat пошкоджений або відсутній. Завантажте мод ще раз і розпакуйте архів повністю.";
        Dictionary<string, object> m = Obj(Obj(man["packs"])["content0"]);
        string src = Path.Combine(Content0, "plpc.w3speech"), dst = Path.Combine(Content0, "brpc.w3speech"), tmp = dst + ".ua_tmp";
        if (!File.Exists(src)) return "не знайдено польської озвучки (content0\\plpc.w3speech). Українська збирається з польського файлу: увімкніть польську мову озвучення в Steam/GOG і дочекайтеся завантаження.";
        Log("перевіряю файли гри (кілька хвилин)...");
        if (W3UA.CrcFile(src) != U(m["orig_crc"]))
        {
            // польський файл змінено ранньою тестовою версією (перезапис польської) — повертаємо оригінал з її копії
            string pb = src + BAK;
            if (File.Exists(pb) && W3UA.CrcFile(pb) == U(m["orig_crc"]))
            {
                File.Delete(src); File.Move(pb, src);
                Log("польську озвучку повернуто до оригіналу (з plpc.w3speech" + BAK + ")");
            }
            else return "польський файл озвучення іншої версії. Ця версія мода — для The Witcher 3 Remastered (5.0). Якщо гра оновилась — зачекайте на оновлення мода або перевірте цілісність файлів у Steam/GOG.";
        }
        bool speechDone = File.Exists(dst) && W3UA.CrcFile(dst) == U(m["new_crc"]);
        long need = speechDone ? 0 : L(m["new_size"]);

        Dictionary<string, object> sb = Obj(man["storybook"]);
        Dictionary<string, string> sbs = new Dictionary<string, string>();
        bool todo = false;
        if (File.Exists(Bundle))
        {
            foreach (string v in sb.Keys)
            {
                Dictionary<string, object> x = Obj(sb[v]);
                uint c = W3UA.MovieCrc(Bundle, (string)x["path"]);
                if (c == U(x["new_crc"])) sbs[v] = "done";
                else if (c == U(x["orig_crc"])) { sbs[v] = "todo"; todo = true; }
                else return "ролик " + v + " іншої версії. Ця версія мода — для The Witcher 3 Remastered (5.0).";
            }
            if (todo && !File.Exists(Bundle + BAK)) need += new FileInfo(Bundle).Length;
        }
        long free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Game))).AvailableFreeSpace;
        if (free < need + (200L << 20))
            return string.Format("замало місця на диску: треба {0:N1} ГБ, вільно {1:N1} ГБ", need / 1073741824.0, free / 1073741824.0);

        if (speechDone) Log("озвучення: вже встановлено");
        else
        {
            object[] e = (object[])m["entries"]; int n = e.Length;
            int[] ei = new int[n], al = new int[n], bl = new int[n]; long[] ao = new long[n], bo = new long[n];
            for (int i = 0; i < n; i++)
            {
                object[] r = (object[])e[i];
                ei[i] = Convert.ToInt32(r[0]); ao[i] = L(r[1]); al[i] = Convert.ToInt32(r[2]); bo[i] = L(r[3]); bl[i] = Convert.ToInt32(r[4]);
            }
            Log("збираю українську озвучку з польського файлу (кілька хвилин)...");
            W3UA.PatchSpeech(src, tmp, dat, ei, ao, al, bo, bl, 0);
            if (W3UA.CrcFile(tmp) != U(m["new_crc"])) { File.Delete(tmp); return "самоперевірка не пройшла; файли гри не змінено."; }
            if (File.Exists(dst))
            {
                // справжній бразильський пакет (якщо був) відкладаємо, а не видаляємо
                if (!File.Exists(dst + BAK)) { File.Move(dst, dst + BAK); Log("знайдено бразильську озвучку — відкладено в brpc.w3speech" + BAK); }
                else File.Delete(dst);
            }
            File.Move(tmp, dst);
            Log(string.Format("озвучення: встановлено, реплік {0}", n));
        }
        if (todo)
        {
            if (!File.Exists(Bundle + BAK)) { Log("резервна копія роликів (7 ГБ)..."); File.Copy(Bundle, Bundle + BAK); }
            foreach (string v in sb.Keys)
            {
                if (sbs[v] != "todo") continue;
                Dictionary<string, object> x = Obj(sb[v]);
                object[] ch = (object[])x["channels"];
                int[] c = new int[ch.Length], h = new int[ch.Length], ln = new int[ch.Length]; long[] of = new long[ch.Length];
                for (int i = 0; i < ch.Length; i++)
                {
                    Dictionary<string, object> y = Obj(ch[i]);
                    c[i] = Convert.ToInt32(y["ch"]); h[i] = Convert.ToInt32(y["hlen"]); of[i] = L(y["off"]); ln[i] = Convert.ToInt32(y["len"]);
                }
                W3UA.PatchMovie(Bundle, (string)x["path"], dat, U(x["orig_crc"]), U(x["new_crc"]), c, h, of, ln);
            }
        }
        if (sbs.Count > 0) Log("ролики оповідача (Любисток): " + sbs.Count + " з " + sb.Count);
        SetLabel(true);
        SetSpeech(true);
        StaleHint();
        return null;
    }

    // Назва пункту меню. Гра не дає модам перекривати власні рядки, тому правимо три рядки у файлі гри.
    // Після оновлення гри Steam/GOG поверне оригінал — тоді пункт знову називатиметься «Бразильська португальська».
    void SetLabel(bool on)
    {
        string f = UaStrings, b = f + BAK;
        if (!File.Exists(f)) { if (on) Log("немає content0\\ua.w3strings — пункт меню лишиться «Бразильська португальська»"); return; }
        try
        {
            byte[] cur = File.ReadAllBytes(f);
            Dictionary<uint, string> now = W3UA.ReadStrings(cur, LABELS.Keys);
            bool ours = now.ContainsKey(1263253) && now[1263253] == LABELS[1263253];
            if (on)
            {
                if (ours) { Log("пункт меню «Українська (ШІ)»: вже"); return; }
                File.Copy(f, b, true);                       // свіжий оригінал (у т.ч. після оновлення гри)
                byte[] nw = W3UA.PatchStrings(cur, LABELS);
                Dictionary<uint, string> chk = W3UA.ReadStrings(nw, LABELS.Keys);
                foreach (uint k in LABELS.Keys) if (!chk.ContainsKey(k) || chk[k] != LABELS[k]) throw new Exception("самоперевірка рядків");
                File.WriteAllBytes(f, nw);
                Log("пункт меню озвучення: «Українська (ШІ)»");
            }
            else
            {
                if (ours && File.Exists(b)) { File.Copy(b, f, true); Log("назву пункту меню повернуто"); }
                if (File.Exists(b)) File.Delete(b);
            }
        }
        catch (Exception ex) { Log("назву пункту меню не змінено (" + ex.Message + ") — у меню він зветься «Бразильська португальська»."); }
    }

    // Мова озвучення в налаштуваннях гри (Документи\The Witcher 3\*.settings, розділ [Localization]).
    // Попереднє значення зберігаємо в ua_voice_prev_speech.txt і повертаємо при видаленні.
    void SetSpeech(bool on)
    {
        string prevFile = Path.Combine(Docs, "ua_voice_prev_speech.txt");
        foreach (string name in new string[] { "dx12user.settings", "user.settings" })
        {
            string f = Path.Combine(Docs, name);
            if (!File.Exists(f)) continue;
            try
            {
                byte[] raw = File.ReadAllBytes(f);
                bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
                string t = new System.Text.UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
                Match cur = Regex.Match(t, @"(?m)^SpeechLanguage=([A-Za-z]*)\r?$");
                string val;
                if (on)
                {
                    if (cur.Success && cur.Groups[1].Value != SLOT && !File.Exists(prevFile)) File.WriteAllText(prevFile, cur.Groups[1].Value);
                    val = SLOT;
                }
                else
                {
                    if (!cur.Success || cur.Groups[1].Value != SLOT) continue;
                    val = File.Exists(prevFile) ? File.ReadAllText(prevFile).Trim() : "PL";
                    if (val.Length == 0 || val == SLOT) val = "PL";
                }
                string t2 = Regex.Replace(t, @"(?m)^(Requested)?SpeechLanguage=[A-Za-z]*(\r?)$", "$1SpeechLanguage=" + val + "$2");
                if (t2 != t)
                {
                    List<byte> o = new List<byte>(); if (bom) o.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
                    o.AddRange(new System.Text.UTF8Encoding(false).GetBytes(t2)); File.WriteAllBytes(f, o.ToArray());
                    Log("налаштування " + name + ": озвучення = " + val);
                }
            }
            catch (Exception ex) { Log("не вдалося змінити " + name + " (" + ex.Message + ") — оберіть мову озвучення в меню гри."); }
        }
        if (!on && File.Exists(prevFile)) File.Delete(prevFile);
    }

    // Після оновлення гри з 4.04 у теках content* лишаються копії від версії мода 0.9 (*.bak_ua).
    // Ремастеру вони не потрібні; самі не видаляємо, лише підказуємо.
    void StaleHint()
    {
        long sz = 0; int cnt = 0;
        try
        {
            foreach (string f in Directory.GetFiles(Path.Combine(Game, "content"), "*.bak_ua", SearchOption.AllDirectories))
            { sz += new FileInfo(f).Length; cnt++; }
        }
        catch { }
        if (cnt > 0) Log(string.Format("Примітка: знайдено {0} старих резервних копій від версії 0.9 для гри 4.04 (*.bak_ua, {1:N1} ГБ) у теці content. Для Remastered вони не потрібні — їх можна видалити вручну.", cnt, sz / 1073741824.0));
    }

    public string Uninstall()
    {
        if (!IsGame(Game)) return "у теці немає гри The Witcher 3: " + Game;
        if (!SkipRunCheck && GameRunning()) return "гра запущена. Закрийте її і спробуйте ще раз.";
        Load();
        int n = 0;
        Dictionary<string, object> m = Obj(Obj(man["packs"])["content0"]);
        string dst = Path.Combine(Content0, "brpc.w3speech");
        if (File.Exists(dst) && W3UA.CrcFile(dst) == U(m["new_crc"])) { File.Delete(dst); n++; Log("озвучення: українську видалено"); }
        else if (File.Exists(dst)) Log("brpc.w3speech не наш — не чіпаю");
        if (!File.Exists(dst) && File.Exists(dst + BAK)) { File.Move(dst + BAK, dst); n++; Log("бразильську озвучку повернуто"); }
        if (File.Exists(Bundle + BAK)) { File.Copy(Bundle + BAK, Bundle, true); File.Delete(Bundle + BAK); n++; Log("ролики оповідача: відновлено"); }
        if (Directory.Exists(ModDir)) { Directory.Delete(ModDir, true); Log("mods\\modUAVoice: видалено"); }   // від ранньої тестової версії
        if (File.Exists(UaStrings + BAK)) n++;
        SetLabel(false);
        SetSpeech(false);
        if (n == 0) return "нічого видаляти: мод не знайдено в цій теці гри.";
        return null;
    }
}
