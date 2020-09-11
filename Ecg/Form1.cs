using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Ecg
{
    public partial class Form1 : Form
    {
        //private string directory = "D:\\Repository\\whitebell\\eratohoNJ\\ERB";
        private string directory;

        private readonly Dictionary<string, ErbFunction> dic = new Dictionary<string, ErbFunction>();

        private readonly Regex func = new Regex("^(@[_\\w\\d]+)(?:\\s*[\\(,])?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Regex callee = new Regex("^(?:TRY(?:C(?:CALL(?:FORM)?|JUMP(?:FORM)?|ALL(?:FORM)?)|JUMP(?:FORM)?)|CALL(?:F(?:ORMF?)?|EVENT)?|JUMP(?:FORM)?|FUNC)\\b\\s*(.+?)(?:\\s*[\\(,].+?)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly Regex begin = new Regex("^BEGIN\\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Regex singlechar = new Regex("\\b[A-Z]\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Regex intInterpolate = new Regex("{.+?}", RegexOptions.Compiled);
        private readonly Regex strInterpolate = new Regex("%.+?%", RegexOptions.Compiled);

        public Form1() => InitializeComponent();

        private void button1_Click(object sender, EventArgs e)
        {
#if DEBUG
            directory = "D:\\Repository\\whitebell\\eratohoNJ\\ERB";
#else
            using (var fsd = new FolderBrowserDialog() { Description = "Select ERB Folder.", SelectedPath = "ERB" })
            {
                if (fsd.ShowDialog() == DialogResult.OK)
                    directory = fsd.SelectedPath;
                else
                    return;
            }
#endif

            string currentLine;
            var sb = new StringBuilder();
            foreach (var file in Directory.EnumerateFiles(directory, "*.ERB", SearchOption.AllDirectories))
            {
                using var sr = new StreamReader(file);
                var currentFunction = "";
                var relPath = file.Remove(0, directory.Length + 1);
                while ((currentLine = sr.ReadLine()?.Trim()) != null)
                {
                    //空行・コメント行はスキップ
                    if (currentLine.Length == 0 || currentLine.StartsWith(';'))
                        continue;

                    //関数宣言行
                    if (currentLine.StartsWith('@'))
                    {
                        currentFunction = func.Match(currentLine).Groups[1].Value;

                        if (dic.ContainsKey(currentFunction))
                        {
                            dic[currentFunction].Filename += $", {relPath}";
                        }
                        else
                        {
                            dic[currentFunction] = new ErbFunction
                            {
                                Name = currentFunction,
                                Filename = relPath,
                            };
                        }
                        continue;
                    }

                    //イベント関数
                    //BEGIN EVENT
                    var m = begin.Match(currentLine);
                    if (m.Success)
                    {
                        var eventfunc = m.Groups[1].Value.ToUpper();
                        switch (eventfunc)
                        {
                            case "TITLE":
                            case "FIRST":
                            case "SHOP":
                            case "TRAIN":
                            case "ABLUP":
                            case "AFTERTRAIN":
                            case "TURNEND":
                            case "LOADGAME":
                            case "SAVEGAME":
                            case "LOADDATAEND":
                                dic[currentFunction].Callee.Add(eventfunc);
                                break;
                            default:
                                MessageBox.Show(eventfunc);
                                break;
                        }
                    }

                    //else
                    m = callee.Match(currentLine);
                    if (m.Success)
                    {
                        dic[currentFunction].Callee.Add($"@{m.Groups[1].Value}");
                    }

                    //single char var
                    m = singlechar.Match(currentLine);
                    if (m.Success)
                    {
                        foreach (var g in m.Groups)
                            dic[currentFunction].SingleCharVariable.Add(g.ToString());
                    }

                    //todo
                }
                //sr.Dispose();
            }

            // event function
            // https://ja.osdn.net/projects/emuera/wiki/flow
            foreach (var key in dic.Keys)
            {
                var dellist = new List<string>();
                var addlist = new List<string>();
                foreach (var callee in dic[key].Callee)
                {
                    switch (callee)
                    {
                        case "TITLE":
                            dellist.Add(callee);
                            if (dic.ContainsKey("@SYSTEM_TITLE"))
                            {
                                addlist.Add("@SYSTEM_TITLE");
                            }
                            else
                            {
                                if (dic.ContainsKey("@TITLE_LOADGAME"))
                                    addlist.Add("@TITLE_LOADGAME");
                                goto first;
                            }
                            break;
                        case "FIRST":
                        first:
                            dellist.Add("FIRST");
                            addlist.Add("@EVENTFIRST");
                            break;
                        case "SHOP":
                            shop:
                            dellist.Add("SHOP");
                            if (dic.ContainsKey("@EVENTSHOP"))
                                addlist.Add("@EVENTSHOP");

                            if (dic.ContainsKey("@SYSTEM_AUTOSAVE"))
                                addlist.Add("@SYSTEM_AUTOSAVE");
                            else if (dic.ContainsKey("@SAVEINFO"))
                                addlist.Add("@SAVEINFO");

                            addlist.Add("@SHOW_SHOP");

                            if (dic.ContainsKey("@USERSHOP"))
                                addlist.Add("@USERSHOP");

                            if (dic.ContainsKey("@EVENTBUY"))
                                addlist.Add("@EVENTBUY");
                            break;
                        case "TRAIN":
                            dellist.Add("TRAIN");
                            if (dic.ContainsKey("@EVENTTRAIN"))
                                addlist.Add("@EVENTTRAIN");
                            if (dic.ContainsKey("@SHOW_STATUS"))
                                addlist.Add("@SHOW_STATUS");

                            addlist.Add("@COM_ABLE{X}");

                            if (dic.ContainsKey("@SHOW_USERCOM"))
                                addlist.Add("@SHOW_USERCOM");

                            if (dic.ContainsKey("@EVENTCOM"))
                                addlist.Add("@EVENTCOM");

                            addlist.Add("@COM{X}");

                            if (dic.ContainsKey("@SOUCE_CHECK"))
                                addlist.Add("@SOUCE_CHECK");

                            if (dic.ContainsKey("@EVENTCOMEND"))
                                addlist.Add("@EVENTCOMEND");

                            if (dic.ContainsKey("@USERCOM"))
                                addlist.Add("@USERCOM");
                            break;
                        case "ABLUP":
                            if (dic.ContainsKey("@SHOW_JUEL"))
                                addlist.Add("@SHOW_JUEL");
                            if (dic.ContainsKey("@SHOW_ABLUP_SELECT"))
                                addlist.Add("@SHOW_ABLUP_SELECT");
                            addlist.Add("@ABLUP{X}");
                            if (dic.ContainsKey("@USERABLUP"))
                                addlist.Add("@USERABLUP");
                            break;
                        case "AFTERTRAIN":
                            dellist.Add("AFTERTRAIN");
                            addlist.Add("@EVENTEND");
                            break;
                        case "TURNEND":
                            dellist.Add("TURNEND");
                            addlist.Add("@EVENTTURNEND");
                            break;
                        case "LOADGAME":
                            dellist.Add("LOADGAME");
                            goto loaddataend;
                        case "SAVEGAME":
                            dellist.Add("SAVEGAME");
                            if (dic.ContainsKey("@SAVEINFO"))
                                addlist.Add("@SAVEINFO");
                            break;
                        case "LOADDATAEND":
                        loaddataend:
                            dellist.Add("LOADDATAEND");
                            if (dic.ContainsKey("@SYSTEM_LOADEND"))
                                addlist.Add("@SYSTEM_LOADEND");
                            if (dic.ContainsKey("@EVENTLOAD"))
                                addlist.Add("@EVENTLOAD");
                            if (dic.ContainsKey("@SHOW_SHOP"))
                                addlist.Add("@SHOW_SHOP");
                            goto shop;
                    }
                }
                foreach (var d in dellist)
                    dic[key].Callee.Remove(d);
                foreach (var a in addlist)
                    dic[key].Callee.Add(a);
            }

            // {INTEGER}, %STRING% interpolation
            {
                var add = new List<ErbFunction>();
                foreach (var key in dic.Keys)
                {
                    foreach (var callee in dic[key].Callee)
                    {
                        //CALLFORM系
                        if (strInterpolate.IsMatch(callee) || intInterpolate.IsMatch(callee))
                        {
                            var f = new ErbFunction() { Name = callee };

                            var pattern = Regex.Escape(callee);
                            pattern = strInterpolate.Replace(pattern, ".+?");
                            pattern = intInterpolate.Replace(pattern, "-?\\d+");
                            var re = new Regex(pattern);

                            foreach (var k in dic.Keys)
                            {
                                if (re.IsMatch(k))
                                {
                                    foreach (var scv in dic[k].SingleCharVariable)
                                        f.SingleCharVariable.Add(scv);
                                }
                            }

                            add.Add(f);
                        }
                    }
                }
                foreach (var func in add)
                    dic[func.Name] = func;
            }

            sb.AppendLine("digraph G {");
            sb.AppendLine("node [shape = record];");
            foreach (var k in dic.Keys)
            {
                if (dic[k].SingleCharVariable.Count != 0)
                {
                    sb.AppendLine(dic[k].Node);

                    if (dic[k].Callee.Count != 0)
                    {
                        foreach (var callee in dic[k].Callee)
                        {
                            if (dic.ContainsKey(callee) && dic[callee].SingleCharVariable.Count != 0)
                                sb.Append('\"').Append(k).Append("\" -> \"").Append(callee).AppendLine("\";");
                        }
                    }
                }
            }
            sb.AppendLine("}");

            textBox1.Text = sb.ToString();
        }
    }

    public class ErbFunction
    {
        private readonly HashSet<string> callee;
        private readonly HashSet<string> singlechar;
        public bool CallForm = false;

        public ErbFunction()
        {
            callee = new HashSet<string>();
            singlechar = new HashSet<string>();
            Name = "";
            Filename = "";
        }

        public ICollection<string> Callee => callee;

        public ICollection<string> SingleCharVariable => singlechar;

        public string Name { get; set; }
        public string Filename { get; set; }

        private string EscapedName => Name.Replace("{", "\\{").Replace("}", "\\}");

        public string Node => singlechar.Count != 0
                    ? $"\"{Name}\" [label = \"{{{EscapedName} | {String.Join(", ", singlechar.OrderBy(e => e))}}}\"]"
                    : $"\"{Name}\" [label = \"{{{EscapedName}}}\"]";
    }
}
