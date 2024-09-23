using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DiepClient.Diep_Encryption;

namespace DiepClient
{
    internal class UpdateClient
    {
        public static readonly string currentfolder = System.AppDomain.CurrentDomain.BaseDirectory;
        public static void Update()
        {
            var idk = GetBuild();
            //var idk = "0470c2b97ccb845ff023";
            GetOffsets(idk);
            //System.Environment.Exit(0);
            CryptoOffsets(idk);
            Debugger.Break();
        }

        static void GetOffsets(string build)
        {
            var offsets = "";
            long function417(string build2)
            {
                int i = 0, seed = 1, res = 0, timer = 0;
                for (; i < build2.Length; i++)
                {
                    var character = int.Parse(build2[i] + "", System.Globalization.NumberStyles.HexNumber);
                    res ^= ((character << ((seed & 1) << 2)) << (timer << 3));
                    timer = (timer + 1) & 3;
                    seed ^= (timer == 0 ? 1 : 0);
                };
                return (uint)res; // unsigned
            }
            offsets += "version: 1.4" + Environment.NewLine;
            offsets += build + Environment.NewLine;
            var all = File.ReadAllText(Path.Combine(currentfolder, build + ".js"));
            var regex = new Regex(@"[$0-9_]+ = [$0-9_]+ \^ \(\([$0-9_]+ & 1 \| 0 \? [$0-9_]+ << 4 \| 0 : [$0-9_]+\) << \([$0-9_]+ << 3 \| 0\) \| 0\) \| 0;");
            var match = regex.Matches(all);
            //if (match.Count > 0)
            //{
            //    var index = all.LastIndexOf("while", match[0].Index);
            //    regex = new Regex(@"\$[0-9]+_[0-9]+ = HEAP8\[\(\$[0-9]+_[0-9]+ \+ ([0-9]+) \| 0\) >> 0\] \| 0;");
            //    var firstmatch = regex.Match(all, index);//@"base64DecodeToExistingUint8Array\(bufferView, "+
            //    match = new Regex(firstmatch.Groups[1].Value + @", ""([a-zA-Z0-9=/+]+)""").Matches(all);
            //    Debug.WriteLine(firstmatch.Groups[1].Value + @", ""([a-zA-Z0-9=/+]+)""");
            //}
            //if (match.Count == 1)
            //{
            //    var actualbuild = string.Join("", Encoding.UTF8.GetString(Convert.FromBase64String(match[0].Groups[1].Value)).Take(40));
            //    offsets += actualbuild + Environment.NewLine;
            //    offsets += function417(actualbuild) + Environment.NewLine;
            //    Debug.WriteLine(offsets);
            //}
            //else
            //{
            var text = all.Substring(all.IndexOf("initActiveSegments"), all.LastIndexOf("base64DecodeToExistingUint8Array") - all.IndexOf("initActiveSegments"));
            //new Regex(", \\\"([a-zA-Z0-9=/+]+)\\\"").Matches(text).Cast<Match>().Where(x => x.Groups.Count>1).ToList().ForEach(x => new string(x.Groups[1].Value.Remove(x.Groups[1].Value.Length-1).Skip(3))));
            var test = new Regex(", \\\"([a-zA-Z0-9=/+]+)\\\"").Matches(text).Cast<Match>().Where(x => x.Groups.Count > 1).Select(x => string.Join("", Encoding.UTF8.GetString(Convert.FromBase64String(x.Groups[1].Value)))).ToList().Select(x => new Regex(@"([a-z0-9]{40})").Matches(x)).Where(x => x.Count > 0).ToList();
            //text = text.Replace("=", "").Replace(" ", "").Replace("\"", "").Replace("(", "").Replace(")", "").Replace("{", "").Replace("}", "").Replace(",", "").Replace("\\", "").Replace(";", "");
            //text = Encoding.UTF8.GetString(Convert.FromBase64String(text));
            //match = new Regex(@"([a-z0-9]{40})").Matches(text);
            //match = new Regex(21392 + @", ""([a-zA-Z0-9=/+]+)""").Matches(all);
            var actualbuild = test.First()[0].Groups[0].Value;
            //test.ForEach(x => { Debug.WriteLine(x[0].Groups[0]); });
            //var actualbuild = "9fa3206fb6f9b2c69e342e0efc926941e988ceb8";
            offsets += actualbuild + Environment.NewLine;
            offsets += function417(actualbuild) + Environment.NewLine;
            Debug.WriteLine(offsets);
            //}
            regex = new Regex(" >> 2] \\| 0;\r\n[ ]+[$0-9_]+ = [$0-9_]+ \\^ ([0-9]+) \\| 0;");//uptime
            match = regex.Matches(all);
            if (match.Count == 1)
            {
                offsets += match[0].Groups[1] + Environment.NewLine;
            }
            //regex = new Regex("[$0-9_]+ = \\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0;");//del
            //regex = new Regex("\\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0(?:;|\\))");
            regex = new Regex("[$0-9_]+ = [$0-9_]+ \\^ \\(\\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0\\) \\| 0;");
            match = regex.Matches(all, match[0].Index);
            if (match.Count == 1)
            {
                offsets += match[0].Groups[1] + Environment.NewLine;
            }
            //regex = new Regex("[$0-9_]+ = [$0-9_]+ \\^ \\(\\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0\\) \\| 0;");//upd
            //regex = new Regex("\\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0(?:;|\\))");
            regex = new Regex("[$0-9_]+ = \\(\\([$0-9_]+ \\+ ([0-9]+) \\| 0\\) & 127 \\| 0\\) \\^ [$0-9_]+ \\| 0;");
            match = regex.Matches(all, match[0].Index);
            if (match.Count == 1)
            {
                offsets += match[0].Groups[1] + Environment.NewLine;
            }
            var fieldupdatefunc = all.Substring(all.LastIndexOf("function", all.IndexOf("case 72:")), all.IndexOf("function", all.IndexOf("case 72:")) - all.LastIndexOf("function", all.IndexOf("case 72:")));
            var fieldgroups = Regex.Matches(fieldupdatefunc, @"(\$\d+_1) = HEAP32\[\(\$0_1 \+ (\d+) \| 0\) >> 2").Cast<Match>();
            var fieldblocks = Regex.Matches(fieldupdatefunc, @"(?<=continue label\$1;(?:.|\n)+?})(?:(?:.|\n)+?continue label\$1;(?:.|\n)+?})").Cast<Match>();
            var objectfuncindex = Regex.Match(all, @"switch \(\$[0-9]+_[0-9]+ - 1 \| 0 \| 0\) {\r\n[ ]+case 16:").Index;
            //var fieldindex = fieldblocks.Select(x => Regex.Matches(x.Value, @"HEAP8\[\((\$\d+_1) \+ (\d+) \| 0\) >> 0\] = 0").Cast<Match>().ToArray());
            var fieldgrouplist = new List<(int, int, string, string)>();//offset, fieldindex, block, variable, 
            foreach (var item in fieldblocks)
            {
                var matches = Regex.Matches(item.Value, @"HEAP8\[\((\$\d+_1) \+ (\d+) \| 0\) >> 0\] = 0;");
                if (matches.Count == 0)
                {
                    matches = Regex.Matches(item.Value, @"HEAP8\[\((i64toi32_i32\$(?:0|1)) \+ (\d+) \| 0\) >> 0] = 0;");
                    if (matches.Count == 1)
                    {
                        //var previousindex = item.Value.LastIndexOf("i64toi32_i32$1 = $", matches[0].Index);
                        //var newstring = item.Value.Remove(previousindex);
                        var previous = Regex.Matches(item.Value, Regex.Escape(matches[0].Groups[1].Value) + @" = (\$\d+_1);(?:.|\n)+?").Cast<Match>().Last(x => x.Index < matches[0].Index);
                        var fieldgroup2 = fieldgroups.First(x => x.Groups[1].Value == previous.Groups[1].Value);
                        var fieldindex = int.Parse(matches[0].Groups[2].Value) - 4;
                        fieldgrouplist.Add(((int.Parse(fieldgroup2.Groups[2].Value) - 72) >> 2, fieldindex, item.Value.Trim(' ', '\r', '\n', ';'), fieldgroup2.Groups[1].Value));
                        continue;
                    }
                    Debugger.Break();
                    //i64toi32_i32\$1 = (\$\d+_1);(?:.|\n)+?
                }
                if (matches.Count == 0)
                    Debugger.Break();
                if (matches.Count > 1)
                    Debugger.Break();
                var fieldgroup = fieldgroups.First(x => x.Groups[1].Value == matches[0].Groups[1].Value);
                fieldgrouplist.Add(((int.Parse(fieldgroup.Groups[2].Value) - 72) >> 2, int.Parse(matches[0].Groups[2].Value) - 4, item.Value.Trim(' ', '\r', '\n', ';'), fieldgroup.Groups[1].Value));
            }
            var temp = new List<(int, int, string, string)>();
            foreach (var item in fieldgrouplist)
            {
                if (temp.Any(x => x.Item1 == item.Item1 && x.Item2 == item.Item2))
                {
                    var item2 = temp[temp.FindIndex(x => x.Item1 == item.Item1 && x.Item2 == item.Item2)];
                    temp[temp.FindIndex(x => x.Item1 == item.Item1 && x.Item2 == item.Item2)] = (item2.Item1, item2.Item2, item2.Item3 + item.Item3, item2.Item4);
                }
                else
                    temp.Add(item);
            }
            fieldgrouplist = temp;
            var objectcreatefunc = all.Substring(all.LastIndexOf("function", objectfuncindex), all.IndexOf("function", objectfuncindex) - all.LastIndexOf("function", objectfuncindex));
            var fieldblocks2 = Regex.Matches(objectcreatefunc, @"(?<=continue label\$2;)(?:(?:.|\n)+?continue label\$2;)").Cast<Match>();
            var fieldgroupswap = new int[fieldblocks2.Count()];
            int fieldgroupswap_index = 0;
            foreach (var item in fieldblocks2)
            {
                var fieldregex = Regex.Match(item.Value, @"if \(HEAP32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] \| 0\) {");
                if (fieldregex.Success)
                {
                    fieldgroupswap[fieldgroupswap_index] = int.Parse(fieldregex.Groups[1].Value);
                }
                else
                {
                    var functionregex = Regex.Match(item.Value, @"\$(\d+)\(\$0_1 \| 0\)");
                    if (functionregex.Success)
                    {
                        var searchstring = "function $" + functionregex.Groups[1].Value + "(";
                        var startfunc = all.IndexOf(searchstring);
                        var newfunction = all.Substring(startfunc, all.IndexOf("function $", startfunc + 1) - startfunc);
                        fieldregex = Regex.Matches(newfunction, @"HEAP32\[\(\$0_1 \+ (\d+) \| 0\) >> 2\] \| 0").Cast<Match>().First(x => x.Groups[1].Value != "48");
                        fieldgroupswap[fieldgroupswap_index] = int.Parse(fieldregex.Groups[1].Value);
                    }
                    else
                        Debugger.Break();
                }
                fieldgroupswap_index++;
            }
            offsets += string.Join(", ", fieldgroupswap.Select(x => (x - 72) >> 2)) + Environment.NewLine;
            foreach (var item in fieldgrouplist.OrderBy(x => x.Item1 * 100 + x.Item2))
            {
                Debug.WriteLine(((item.Item1 << 2) + 72) + " " + item.Item4 + " " + item.Item1 + "_" + item.Item2);
            }
            File.WriteAllText("parsedthing.txt", string.Join("\n", fieldgrouplist.OrderBy(x => x.Item1 * 100 + x.Item2).Select(x => x.Item4 + ": " + x.Item1 + "_" + x.Item2 + "\n" + Regex.Replace(x.Item3, @"^ +", "", RegexOptions.Multiline).Replace("\r\n", ""))));
            offsets += FieldOrderOffsets(fieldgrouplist, all, build);
            File.WriteAllText(INFO.buildfile, offsets);
        }
        public static string FieldOrderOffsets(List<(int, int, string, string)> parse, string all, string build)
        {
            int Owner = -1, Barrel = -1, Physics = -1, Health = -1, Unknown = -1, Scores = -1, Arena = -1, Name = -1, GUI = -1, Position = -1, Display = -1, Score = -1, Mothership = -1, PlayerInfo = -1;
            /*7_11 6, 7_12 6, 8_0 5, 9_3 5, 9_6 5, 9_8 5, 9_17 5, 10_3: 1, 15_0 6, 15_1 6*/
            var completeparse2 = new List<(int, int, string, string)>();//fieldgroup, index, type, name
            var completeparse3 = new List<(int, int, string, string)>();//fieldgroup, index, type, name
            foreach (var item in parse)
            {
                var type = "";
                var functionamount = Regex.Matches(item.Item3, "\\$\\d+\\(").Count;
                if (item.Item3.Contains(" & 65535"))// || item.Item1 == 0)
                    type = "entid";
                else if (item.Item3.Contains("wasm2js_i32"))
                    type = "string";
                else if (item.Item3.IndexOf(" = Math_fround(0.0);") > 0 && item.Item3.IndexOf(" = Math_fround(0.0);") < 10)
                    type = "float";
                else if (item.Item3.Contains("wasm2js_scratch_load_f32"))
                    type = "float";
                else if (item.Item3.Contains(") & 266338304") && Regex.Match(item.Item3, @"\(0 - \(\$[0-9]+_[0-9]+ & 1 \| 0\) \| 0\) \^ \(\$[0-9]+_[0-9]+ >>> 1 \| 0\)").Success)
                    type = "long";
                else if (item.Item3.Contains(") & 266338304"))
                    type = "ulong";
                if (item.Item3.Contains("while (1)") && (type != "string" || type == "string" && Regex.Matches(item.Item3, "while").Count > 1))
                {
                    if (item.Item3.Contains("Math_fround") && item.Item3.Contains("HEAPF32"))
                    {
                        type = "float";
                    }
                    type += "[]";
                    if (type == "[]")
                        type = "string";
                }
                if (functionamount >= 5 && type != "string" && type.Contains("[]"))
                    type = "string[]";
                if (type == "")
                    Debugger.Break();
                completeparse2.Add((item.Item1, item.Item2, type, item.Item3));
            }
            var groups = completeparse2.GroupBy(x => x.Item1);
            foreach (var fieldgroup in groups)
            {
                var items = fieldgroup.Count();
                if (items == 3 && fieldgroup.All(x => x.Item3 == "entid"))
                    Owner = fieldgroup.Key;
                else if (items == 3 && fieldgroup.Any(x => x.Item3 == "ulong") && fieldgroup.Count(x => x.Item3 == "float") == 2)
                {
                    foreach (var item in fieldgroup)
                    {
                        var match = Regex.Match(item.Item4, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ \(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ \d+ \| 0\) >> 0] \| 0 \? \d+ : (\d+)\) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;");
                        var matches2 = Regex.Matches(all, @"Math_fround\(\$[0-9]+_[0-9]+ \* \$[0-9]+_[0-9]+ \* \(3.0 - \(\$[0-9]+_[0-9]+ \+ \$[0-9]+_[0-9]+\)\) \* \+Math_fround\(Math_fround\(HEAPF32\[\((\$[0-9]+_[0-9]+) \+ " + match.Groups[1].Value + @" \| 0\) >> 2\]\) - \$[0-9]+_[0-9]+\) \+ \+\$[0-9]+_[0-9]+\);").Cast<Match>();
                        bool found = false;
                        foreach (var match2 in matches2)
                        {
                            var variable = match2.Groups[1].Value;
                            var function = all.Substring(all.LastIndexOf("function", match2.Index), all.IndexOf("function", match2.Index) - all.LastIndexOf("function", match2.Index));
                            var regex = "\\" + variable + @" = HEAP32\[\(\$[0-9]+_[0-9]+ \+ " + ((item.Item1 << 2) + 72) + @" \| 0\) >> 2\] \| 0;";
                            var match3 = Regex.Match(function, regex);
                            if (match3.Success)
                            {
                                Debug.WriteLine(match3.Value);
                                found = true;
                                break;
                            }
                        }
                        if (found)
                            Health = fieldgroup.Key;
                        else
                            Barrel = fieldgroup.Key;
                    }
                }
                else if (items == 6)
                    Physics = fieldgroup.Key;
                else if (items == 1 && fieldgroup.First().Item3 == "long")
                    Unknown = fieldgroup.Key;
                else if (items == 12)
                    Arena = fieldgroup.Key;
                else if (items == 2)
                    Name = fieldgroup.Key;
                else if (items == 23)
                    GUI = fieldgroup.Key;
                else if (items == 3 && fieldgroup.Any(x => x.Item3 == "float[]"))
                    Scores = fieldgroup.Key;
                else if (items == 4 && fieldgroup.Count(x => x.Item3 == "long") == 3)
                    Position = fieldgroup.Key;
                else if (items == 5)
                    Display = fieldgroup.Key;
                else if (items == 1 && fieldgroup.First().Item3 == "float")
                    Score = fieldgroup.Key;
                else if (items == 4 && fieldgroup.Count(x => x.Item3 == "float") == 2)
                    Mothership = fieldgroup.Key;
                else if (items == 3 && fieldgroup.Count(x => x.Item3 == "string[]") == 2)
                    PlayerInfo = fieldgroup.Key;
                else
                {
                    Debug.WriteLine(items + " " + string.Join(", ", fieldgroup.Select(x => x.Item3)));
                    Debugger.Break();
                }
            }
            foreach (var item in completeparse2)
            {
                var type = item.Item3;
                var name = "";
                if (item.Item1 == Owner)
                {
                    var match = Regex.Matches(item.Item4, @"HEAP16\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 1\] = \$[0-9]+_[0-9]+;").Cast<Match>().Last();
                    var matches = Regex.Matches(all, @"= HEAP32\[\(\(\(HEAPU16\[\(\$[0-9]+_[0-9]+ \+ " + match.Groups[1].Value + @" \| 0\) >> 1\] \| 0\) << 2 \| 0\) \+ \d+ \| 0\) >> 2\] \| 0;");
                    //Debug.WriteLine(matches.Count + " " + item.Item2);
                    if (matches.Count > 20)
                        name = "parent";
                }
                if (item.Item1 == Barrel && type == "ulong")
                {
                    name = "shooting";
                }
                //else if (item.Item1 == Barrel && type == "float" && item.Item2 == 0)
                //{
                //    name = "reloadTime";
                //}
                if (item.Item1 == Physics && type == "ulong")
                {
                    var match = Regex.Matches(item.Item4, @"HEAP32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;").Cast<Match>().Last();
                    var matches = Regex.Matches(all, @"\$[0-9]+_[0-9]+ = HEAP32\[\(\$[0-9]+_[0-9]+ \+ " + match.Groups[1].Value + @" \| 0\) >> 2\] \| 0;\r\n[ ]+if \(!\(\(\$[0-9]+_[0-9]+ \| 0\) != \(1 \| 0\) & \(\$[0-9]+_[0-9]+ \| 0\) < \(3 \| 0\) \| 0\)\) {");
                    if (matches.Count > 0)
                        name = "sides";
                    else
                        name = "Object";
                }
                else if (item.Item1 == Physics && type == "float" && item.Item4.Contains("100.0"))
                {
                    var match = Regex.Matches(item.Item4, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;").Cast<Match>().Last();
                    var matches = Regex.Matches(all, @"\$[0-9]+_[0-9]+ = Math_fround\(HEAPF32\[\((\$[0-9]+_[0-9]+) \+ " + match.Groups[1].Value + @" \| 0\) >> 2\]\)");
                    var infunction = matches.Cast<Match>().Where(x => Regex.Match(all.Substring(all.LastIndexOf("function", x.Index), all.IndexOf("function", x.Index) - all.LastIndexOf("function", x.Index)), @"\" + x.Groups[1].Value + @" = HEAP32\[\(\$[0-9]+_[0-9]+ \+ " + ((item.Item1 << 2) + 72) + @" \| 0\) >> 2\] \| 0;").Success).ToList();
                    if (infunction.Count > 15)
                        name = "size";
                    else
                        name = "width";
                }
                if (item.Item1 == Health && type == "ulong")
                {
                    name = "healthbar";
                }
                else if (item.Item1 == Health && type == "float")
                {
                    var match = Regex.Match(item.Item4, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ \(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ \d+ \| 0\) >> 0] \| 0 \? \d+ : (\d+)\) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;");
                    var matches2 = Regex.Matches(all, @"Math_fround\(\$[0-9]+_[0-9]+ \* \$[0-9]+_[0-9]+ \* \(3.0 - \(\$[0-9]+_[0-9]+ \+ \$[0-9]+_[0-9]+\)\) \* \+Math_fround\(Math_fround\(HEAPF32\[\((\$[0-9]+_[0-9]+) \+ " + match.Groups[1].Value + @" \| 0\) >> 2\]\) - \$[0-9]+_[0-9]+\) \+ \+\$[0-9]+_[0-9]+\);").Cast<Match>();
                    bool found = false;
                    foreach (var match2 in matches2)
                    {
                        var variable = match2.Groups[1].Value;
                        var function = all.Substring(all.LastIndexOf("function", match2.Index), all.IndexOf("function", match2.Index) - all.LastIndexOf("function", match2.Index));
                        var regex = "\\" + variable + @" = HEAP32\[\(\$[0-9]+_[0-9]+ \+ " + ((item.Item1 << 2) + 72) + @" \| 0\) >> 2\] \| 0;";
                        var match3 = Regex.Match(function, regex);
                        if (match3.Success)
                        {
                            Debug.WriteLine(match3.Value);
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        name = "health";
                    else
                        name = "maxHealth";
                }
                if (item.Item1 == Unknown && type == "long")
                {
                    name = "unknown";
                }
                if (item.Item1 == Arena && type == "ulong[]")
                {
                    name = "scoreboardColors";
                }
                else if (item.Item1 == Arena && type == "long[]")
                {
                    name = "scoreboardTanks";
                }
                else if (item.Item1 == Arena && type == "long")
                {
                    name = "playersNeeded";
                }
                else
                {

                }
                //else if (item.Item1 == 7 && type == "float" && item.Item2 == 0)
                //{
                //    name = "arenaLeftX";
                //}
                //else if (item.Item1 == 7 && type == "ulong" && item.Item2 == 4)
                //{
                //    name = "scoreboardAmount";
                //}
                if (item.Item1 == Name && type == "string")
                {
                    name = "name";
                }
                if (item.Item1 == Name && type == "ulong")
                {
                    name = "nametag";
                }
                if (item.Item1 == GUI && type == "entid")
                {
                    name = "player";
                }
                if (item.Item1 == GUI && type == "long[]" && !completeparse3.Any(x => x.Item4 == "statMaxes"))
                {
                    name = "statMaxes";
                }
                else if (item.Item1 == GUI && type == "long[]")
                {
                    name = "statLevels";
                }
                else if (item.Item1 == GUI && type == "float" && item.Item4.Contains(" | 0) >> 0] | 0)) {HEAP8[("))
                {
                    name = "";
                }
                else if (item.Item1 == GUI && type == "float")// item.Item4.Contains(" | 0) >> 0] | 0)) {HEAP8[("))
                {
                    var number = Regex.Match(item.Item4, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;");
                    if (Regex.IsMatch(all, @"\$[0-9]+_[0-9]+ = HEAP32\[\$[0-9]+_[0-9]+ >> 2\] \| 0;\r\n[ ]+if \(!\$[0-9]+_[0-9]+\) {\r\n[ ]+break label\$[0-9]+\r\n[ ]+}\r\n[ ]+\$[0-9]+_[0-9]+ = Math_fround\(HEAPF32\[\(\$[0-9]+_[0-9]+ \+ " + number.Groups[1].Value + @" \| 0\) >> 2\]\);"))
                    {
                        name = "FOV";
                    }
                }
                //else if (item.Item1 == 9 && type == "long")//14
                //{
                //    name = "level";
                //}
                //else if (item.Item1 == 9 && item.Item2 == 12)
                //{
                //    name = "cameraX";
                //}
                //else if (item.Item1 == 9 && item.Item2 == 7)
                //{
                //    name = "cameraY";
                //}
                if (item.Item1 == Scores && type == "float[]")
                {
                    name = "scoreboardScores";
                }
                else if (item.Item1 == Scores && type == "float")
                {
                    if (completeparse3.Any(x => x.Item4 == "leaderX"))
                    {
                        name = "leaderY";
                    }
                    else if (completeparse3.Any(x => x.Item4 == "leaderY"))
                    {
                        name = "leaderX";
                    }
                    else
                    {
                        var match = Regex.Matches(item.Item4, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;").Cast<Match>().Last();
                        var testregex = Regex.Match(all, @"HEAPF32\[\(\$[0-9]+_[0-9]+ \+ " + match.Groups[1].Value + @" \| 0\) >> 2\] = \$[0-9]+_[0-9]+;\r\n[ ]+HEAP8\[\(\$[0-9]+_[0-9]+ \+ \d+ \| 0\) >> 0\] = 0;\r\n[ ]+\$[0-9]+_[0-9]+ = \$[0-9]+\(\$[0-9]+_[0-9]+ \| 0");
                        if (testregex.Success)
                            name = "leaderX";
                        else
                            name = "leaderY";
                    }
                }
                if (item.Item1 == Position && item.Item4.Contains(".015625"))
                {
                    name = "angle";
                }
                else if (item.Item1 == Position && type == "ulong")
                {
                    name = "motion";
                }
                else if (item.Item1 == Position && type == "long" && !completeparse3.Any(x => x.Item4 == "y"))//FIX
                {
                    name = "y";
                }
                else if (item.Item1 == Position && type == "long")
                {
                    name = "x";
                }
                if (item.Item1 == Display && type == "long")
                {
                    name = "borderThickness";
                }
                else if (item.Item1 == Display && type == "float")
                {
                    name = "opacity";
                }
                else if (item.Item1 == Display && type == "ulong" && item.Item4.Contains("HEAPF64"))
                {
                    name = "style";
                }
                else if (item.Item1 == Display && type == "ulong")
                {
                    if (completeparse3.Any(x => x.Item4 == "color"))
                    {
                        name = "serverEntityCount";
                    }
                    else if (completeparse3.Any(x => x.Item4 == "serverEntityCount"))
                    {
                        name = "color";
                    }
                    else
                    {
                        var match = Regex.Matches(item.Item4, @"HEAP32\[\(\$[0-9]+_[0-9]+ \+ (\d+) \| 0\) >> 2\] = \$[0-9]+_[0-9]+;").Cast<Match>().Last();
                        var matches = Regex.Matches(all, @"HEAP32\[\((\$[0-9]+_[0-9]+) \+ " + match.Groups[1].Value + @" \| 0\) >> 2\]").Cast<Match>();
                        foreach (var x in matches)
                        {
                            var index = all.LastIndexOf("function", x.Index);
                            if (x.Index - index > 6000)
                                continue;
                            var match2 = Regex.Match(all.Substring(index, x.Index - index), @"\" + x.Groups[1].Value + @" = HEAP32\[\(\$[0-9]+_[0-9]+ \+ " + ((item.Item1 << 2) + 72) + @" \| 0\) >> 2\] \| 0;");
                            if (match2.Success)
                            {
                                name = "serverEntityCount";
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(name))
                            name = "color";
                    }
                }
                if (item.Item1 == Score && type == "float")
                {
                    name = "score";
                }
                if (item.Item1 == PlayerInfo && type == "ulong")
                {
                    name = "playercount";
                }
                else if (item.Item1 == PlayerInfo && type == "string[]")
                {
                    name = "playerid_or_name";
                }
                //Debug.WriteLine((item.Item1, item.Item2, type));
                completeparse3.Add((item.Item1, item.Item2, type, name));
            }
            Debug.WriteLine(string.Join("\n", completeparse3.OrderBy(x => x.Item1 * 100 + x.Item2).Select(x => x.Item4 + " " + x.Item3 + ": " + x.Item1 + "_" + x.Item2)));
            File.WriteAllText("fieldorder2" + build + ".txt", JsonConvert.SerializeObject(completeparse3, Formatting.Indented));
            File.WriteAllText("fieldordersorted" + build + ".txt", JsonConvert.SerializeObject(completeparse3.OrderBy(x => x.Item1 * 100 + x.Item2), Formatting.Indented));
            return (Owner, Barrel, Physics, Health, Unknown, Scores, Arena, Name, GUI, Position, Display, Score, Mothership, PlayerInfo) + Environment.NewLine + JsonConvert.SerializeObject(completeparse3) + Environment.NewLine;
        }
        public static void CryptoOffsets(string build)
        {
            Debug.WriteLine("idk");
            var save = new CipherSave();
            var all = File.ReadAllText(Path.Combine(currentfolder, build + ".js"));
            //var regex = new Regex(@" >>> 0\) % \(\(");
            //var regex = new Regex(@"\(\(\$[0-9]+_[0-9]+ >>> 0\) % \(");
            var regex = new Regex(@" >>> 0\) \| 0\)");
            //var match = regex.Matches(all);
            //regex = new Regex(@"\(\(\$[0-9]+_[0-9]+ >>> 0\) % \(\$[0-9]+_[0-9]+ >>> 0\) \| 0\) \+ \$[0-9]+_[0-9]+ \| 0;");
            //var mmatch2 = regex.Matches(all);
            //if (match.Count + mmatch2.Count > 0)
            //{
            var sbox_regex4 = new Regex(@"\$[0-9]+_1 = 127;\r\n[ ]+\$[0-9]+_1 = 126;");
            var sbox_match4 = sbox_regex4.Match(all);
            var index = sbox_match4.Index;
            var start = all.LastIndexOf("function $", index);
            var funcend = all.IndexOf("function $", index);
            var functionstring = all.Substring(start, funcend - start);
            var tablematch = regex.Match(functionstring).Index;
            var seed_regex = new Regex(@"HEAP32\[\$[0-9]+_1 >> 2\] = \(wasm2js_i32\$[0-9]+ = HEAP32\[\(\$[0-9]+_1 \+ [0-9]+ \| 0\) >> 2\] \| 0, wasm2js_i32\$[0-9]+ = \$[0-9]+_1 \+ [0-9]+ \| 0, wasm2js_i32\$[0-9]+ = \(HEAP8\[\(\$[0-9]+_1 \+ [0-9]+ \| 0\) >> 0\] \| 0 \| 0\) < \(0 \| 0\), wasm2js_i32\$[0-9]+ \? wasm2js_i32\$[0-9]+ : wasm2js_i32\$[0-9]+\);\r\n[ ]+HEAP32\[\(\$[0-9]+_1 \+ 4 \| 0\) >> 2\] = fimport\$[0-9]+\([0-9]+ \| 0, [0-9]+ \| 0, \$[0-9]+_1 \| 0\) \| 0;");
            var seed_match = seed_regex.Match(all);
            var sbox_seeds = "";
            var sbox_index = 0;
            int skip = 0;
            Tuple<int, int> FindSeed(string variable, int seed)
            {
                if (seed == 0)
                {
                    var seed_regex3 = new Regex(variable.Replace("$", "\\$") + @" = HEAP32\[\([0-9$_]+ \+ ([0-9]+) \| 0\) >> 2] \| 0;");//\r\n[ ]+\$
                    var sub = all.Substring(start + skip, funcend - start - skip);
                    if (skip > 0)
                        seed_regex3 = new Regex("        " + variable.Replace("$", "\\$") + @" = HEAP32\[\([0-9a-z$_]+ \+ ([0-9]+) \| 0\) >> 2] \| 0;");
                    var seed_match3 = seed_regex3.Matches(sub).Cast<Match>().ToList();
                    if (seed_match3.Count == 0)
                    {
                        seed_regex3 = new Regex(variable.Replace("$", "\\$") + @" = HEAP32\[\(i64toi32_i32\$[0-9]+ \+ ([0-9]+) \| 0\) >> 2\] \| 0;");
                        seed_match3 = seed_regex3.Matches(sub).Cast<Match>().ToList();
                        if (seed_match3.Count == 0)
                            return new Tuple<int, int>(0, 0);
                    }
                    if (seed_match3.Count > 1)
                    {
                        Debug.WriteLine(seed_match3.Count);
                        Debugger.Break();
                        seed_match3 = new List<Match>() { seed_match3.First(x => int.Parse(x.Groups[1].Value) == seed_match3.Max(s => int.Parse(s.Groups[1].Value))) };
                    }
                    var seed_index = seed_match3.First().Groups[1].Value;
                    //var seed_regex1 = new Regex($" \\+ {seed_index} \\| 0\\) >> 2] = ([-0-9]+);");
                    //var seed_match1 = seed_regex1.Match(all.Substring(seed_match.Index, 10000));
                    //if (seed_match1.Success)
                    //    return new Tuple<int, int>(int.Parse(seed_index), int.Parse(seed_match1.Groups[1].Value));
                    //
                    var seed_regex2 = new Regex($" \\+ {seed_index} \\| 0\\) >> 2] = ([-$0-9a-z_]+);");
                    var seed_matche2 = seed_regex2.Matches(all.Substring(seed_match.Index, 10000)).Cast<Match>();
                    foreach (var seed_match2 in seed_matche2)
                    {
                        if (int.TryParse(seed_match2.Groups[1].Value, out int value))
                            if (value != 0)
                                return new Tuple<int, int>(int.Parse(seed_index), int.Parse(seed_match2.Groups[1].Value));
                        var corrected = seed_match2.Groups[1].Value.Replace("$", "\\$");
                        seed_regex2 = new Regex($" {corrected} = ([-0-9]+);");
                        var seed_matches2 = seed_regex2.Matches(all.Substring(seed_match.Index, seed_match2.Index));
                        if (seed_matches2.Count > 0)
                            if (int.Parse(seed_matches2[seed_matches2.Count - 1].Groups[1].Value) != 0)
                                return new Tuple<int, int>(int.Parse(seed_index), int.Parse(seed_matches2[seed_matches2.Count - 1].Groups[1].Value));
                    }
                    return new Tuple<int, int>(int.Parse(seed_index), 0);
                }
                else if (seed == 1)
                {
                    var seed_regex2 = new Regex(variable.Replace("$", "\\$") + @" = ([-0-9]+);");
                    var seed_match2 = seed_regex2.Match(sbox_seeds);
                    if (seed_match2.Success)
                        return new Tuple<int, int>(0, int.Parse(seed_match2.Groups[1].Value));
                }
                else if (seed == 2)
                {
                    var last_index = all.LastIndexOf(variable, sbox_index);
                    var seed_regex2 = new Regex(variable.Replace("$", "\\$") + @" = ([-0-9]+);");
                    var seed_match2 = seed_regex2.Match(all.Substring(last_index, 200));
                    if (seed_match2.Success)
                        return new Tuple<int, int>(0, int.Parse(seed_match2.Groups[1].Value));
                }
                return new Tuple<int, int>(0, 0);
            }
            Diep_Encryption.PRNG ExtractClass(string text, string variable = "", int seed = 0)
            {
                var secondtext = "";
                if (seed == 4)
                {
                    var index543 = text.IndexOf("HEAP8", 1);
                    secondtext = text.Substring(index543, text.Length - index543);
                    text = text.Substring(0, index543);
                    seed = 0;
                }
                if (text.Contains(" >>> ") && text.Contains(" << ") && text.Contains(" | 0) | 0") && text.Contains(" | 0) ^ ") && new Regex(@"(\$[0-9_]+) << (\d+) \| 0( \||\) \+) (\d+)").Match(text).Success)
                {
                    var regex1 = new Regex(@"(\$[0-9_]+) << (\d+) \| 0( \||\) \+) (\d+)");
                    var match1 = regex1.Matches(text);
                    var regex2 = new Regex(@"(\$[0-9_]+) >>> (\d+) \| 0( \||\) \+) (\d+)");
                    var match2 = regex2.Matches(text);
                    var seedfind = FindSeed(match1[0].Groups[1].Value, seed);
                    var shifts = new int[match1.Count + match2.Count];
                    var adds = new bool[match1.Count + match2.Count];
                    var values = new int[match1.Count + match2.Count];
                    var current1 = 0;
                    var current2 = 0;
                    for (int i = 0; i < shifts.Length; i++)
                    {
                        if (i == 1 || i == 6)
                        {
                            shifts[i] = int.Parse(match2[current2].Groups[2].Value);
                            adds[i] = match2[current2].Groups[3].Value != " |";
                            values[i] = int.Parse(match2[current2].Groups[4].Value);
                            current2++;
                        }
                        else
                        {
                            shifts[i] = int.Parse(match1[current1].Groups[2].Value);
                            adds[i] = match1[current1].Groups[3].Value != " |";
                            values[i] = int.Parse(match1[current1].Groups[4].Value);
                            current1++;
                        }
                    }
                    return new Diep_Encryption.OneShiftAdd(seedfind.Item2, shifts, adds, values) { variable = match1[0].Groups[1].Value, index = seedfind.Item1 };
                }
                else if (text.Contains(" >>> ") && text.Contains(" << ") && new Regex(@"(\$[$0-9_]+) = \((\$[$0-9_]+) << (\d+) \| 0\) \^ ").Match(text).Success)// && new Regex("(" + Regex.Escape(variable) + @") << ([0-9]+)").Matches(text).Count > 0)
                {
                    //if (text.Contains("HEAP8["))
                    //    text = text.Substring(0, text.IndexOf("HEAP8["));
                    var regex2 = new Regex(@"(\$[$0-9_]+) = \((\$[$0-9_]+) << (\d+) \| 0\) \^ ");
                    var matches2 = regex2.Matches(text).Cast<Match>();
                    var regex3 = new Regex(@"\([$0-9_]+ >>> ([0-9]+) \| 0\) \^ ");
                    var matches3 = regex3.Matches(text).Cast<Match>();
                    var setvariales = matches2.Where(x => x.Groups[1].Value != x.Groups[2].Value).Select(x => x.Groups[1].Value).ToList();
                    var settedvariales = matches2.Select(x => x.Groups[1].Value).ToList();
                    //var templist2 = matches2.Select(x => templist.IndexOf(x.Groups[2].Value)).ToList();
                    var variables = matches2.Where((x, i) => !settedvariales.Contains(x.Groups[2].Value) || i <= settedvariales.IndexOf(x.Groups[2].Value)).GroupBy(x => x.Groups[2].Value).Select(x => x.First()).ToArray();
                    var seeds = variables.Select(x => FindSeed(x.Groups[2].Value, seed)).ToArray();
                    var values2 = matches2.Select(x => int.Parse(x.Groups[3].Value)).ToArray();
                    var values3 = matches3.Select(x => int.Parse(x.Groups[1].Value)).ToArray();
                    var extravalue = string.IsNullOrWhiteSpace(secondtext) ? 0 : int.Parse(regex2.Match(secondtext).Groups[3].Value);
                    if (variables.Length == 0)
                    {
                        seeds = new[] { FindSeed(matches2.First().Groups[2].Value, seed) };
                        return new Diep_Encryption.XorShift(seeds[0].Item2, values2, values3, extravalue) { variable = matches2.First().Groups[2].Value, index = seeds[0].Item1 };
                    }
                    if (variables.Length == 1)
                        return new Diep_Encryption.XorShift(seeds[0].Item2, values2, values3, extravalue) { variable = variables.First().Groups[2].Value, index = seeds[0].Item1 };
                    else if (variables.Length == 3)
                        return new Diep_Encryption.TripleXorShift(seeds[0].Item2, seeds[1].Item2, seeds[2].Item2, values2, values3)
                        {
                            variable = variables[0].Groups[2].Value,
                            variable2 = variables[1].Groups[2].Value,
                            variable3 = variables[2].Groups[2].Value,
                            index = seeds[0].Item1,
                            index2 = seeds[1].Item1,
                            index3 = seeds[2].Item1,
                        };
                    else
                        Debugger.Break();
                }
                //else if (text.Contains(" >>> ") && text.Contains(" << ") && new Regex("(" + Regex.Escape(variable) + @") << ([0-9]+)").Matches(text).Count > 0)
                //{
                //    Debugger.Break();
                //    //var regex2 = new Regex(@"([$0-9_]+) << ([0-9]+)");
                //    //var matches2 = regex2.Matches(text);
                //    //var regex3 = new Regex(@"[$0-9_]+ >>> ([0-9]+)");
                //    //var matches3 = regex3.Matches(text);
                //    //var seedfind = FindSeed(matches2[0].Groups[1].Value, seed);
                //    //var values2 = matches2.Cast<Match>().Select(x => int.Parse(x.Groups[2].Value)).ToArray();
                //    //var values3 = matches3.Cast<Match>().Select(x => int.Parse(x.Groups[1].Value)).ToArray();
                //    //return new Diep_Encryption.XorShift(seedfind.Item2, values2, values3) { variable = matches2[0].Groups[1].Value, index = seedfind.Item1 };
                //}
                else if (text.Contains("__wasm_i64_urem"))
                {
                    var mul_regex = new Regex(@"__wasm_i64_mul\(([$0-9_]+) \| 0, [$0-9a-z_]+ \| 0, ([0-9]+) \| 0, [$0-9a-z_]+ \| 0\) \| 0;");
                    var mul_match = mul_regex.Match(text);
                    var add_regex = new Regex(@"[$0-9a-z_]+ = ([1-9][0-9]*);");
                    var add_match = add_regex.Match(text);
                    var mod_regex = new Regex(@"[$0-9a-z_]+ = __wasm_i64_urem\([$0-9a-z_]+ \| 0, [$0-9a-z_]+ \| 0, ([0-9]+) \| 0, [$0-9a-z_]+ \| 0\) \| 0;");
                    var mod_match = mod_regex.Match(text);
                    var seedfind = FindSeed(mul_match.Groups[1].Value, seed);
                    return new Diep_Encryption.LCG(seedfind.Item2, long.Parse(mul_match.Groups[2].Value), long.Parse(add_match.Groups[1].Value), long.Parse(mod_match.Groups[1].Value)) { variable = mul_match.Groups[1].Value, index = seedfind.Item1 };
                }
                else if (text.Contains("Math_imul"))
                {
                    var regex2 = new Regex(@"[$0-9_]+ = Math_imul\(([$0-9_]+), ([0-9]+)\) \+ ([0-9]+) \| 0;");
                    var matches2 = regex2.Matches(text).Cast<Match>().ToList();
                    if (matches2.Count != 3)
                        matches2.AddRange(new Regex(@"Math_imul\(([$0-9_]+), ([0-9]+)\) \| 0\) \+ ([0-9]+)").Matches(text).Cast<Match>());
                    if (matches2.Count < 3)
                        throw new Exception();
                    var seedfind1 = FindSeed(matches2[0].Groups[1].Value, seed);
                    var seedfind2 = FindSeed(matches2[1].Groups[1].Value, seed);
                    var seedfind3 = FindSeed(matches2[2].Groups[1].Value, seed);
                    return new Diep_Encryption.TripleLCG(seedfind1.Item2, int.Parse(matches2[0].Groups[2].Value), int.Parse(matches2[0].Groups[3].Value), seedfind2.Item2, int.Parse(matches2[1].Groups[2].Value), int.Parse(matches2[1].Groups[3].Value), seedfind3.Item2, int.Parse(matches2[2].Groups[2].Value), int.Parse(matches2[2].Groups[3].Value)) { variable = matches2[0].Groups[1].Value, variable2 = matches2[1].Groups[1].Value, variable3 = matches2[2].Groups[1].Value, index = seedfind1.Item1, index2 = seedfind2.Item1, index3 = seedfind3.Item1 };
                }
                return null;
            }
            Diep_Encryption.PRNG ExtractHeaderSubstitutionCount(string variable, string text)
            {
                if (false && skip == 0)
                {
                    var index3 = text.LastIndexOf(variable + @" = ");

                    //var regex2 = new Regex();
                    //var match2 = Regex.Matches(text, @"HEAP8\[\(\$[0-9]+_1 \+ 3 \| 0\) >> 0\] = \(HEAPU8\[\(\$[0-9]+_1 \+ 3 \| 0\) >> 0\] \| 0\) \^ \(Math_imul\(\$[0-9]+_1, 27\) \+ 1 \| 0\) \| 0;");
                    //var startidk = text.LastIndexOf(variable + @" = ", text.Length);
                    //startidk = text.LastIndexOf(variable + @" = ", startidk-200)-150;
                    Regex test2;


                    var test = new Regex(@"\" + variable + @" = (\$[0-9_]+);").Matches(text).Cast<Match>();
                    test2 = new Regex(@"([$0-9a-z_]+) = [a-zA-Z0-9\(\)_\<\>\|\,\$]+?\" + test.First().Groups[1].Value);

                    //else
                    //    test2 = new Regex(@"\" + variable + @" = (?!\$\" + variable + @")");
                    var test3 = test2.Match(text);
                    //match2 = Regex.Match(text, @"\"+match2.Groups[1] + " = ", RegexOptions.RightToLeft);
                    //var startidk = text.LastIndexOf("Math_imul", match2[match2.Count - 2].Index);
                    //var idk = text.LastIndexOf("label", test3.Index);
                    var startidk = text.LastIndexOf("label", test3.Index);
                    var idk = text.IndexOf("label", test3.Index);
                    //var idk2 = text.LastIndexOf("label$19 : {", idk - 7);
                    //if (idk2 != -1)
                    //    idk = idk2;
                    //var headerSubstitutionCount_text = text.Substring(idk, startidk - idk);
                    var headerSubstitutionCount_text = text.Substring(startidk, idk - startidk);
                    return ExtractClass(headerSubstitutionCount_text, test.First().Groups[1].Value, 0);
                }
                else
                {
                    var match2 = Regex.Matches(text, @"HEAP8\[\(\$[0-9]+_1 \+ 3 \| 0\) >> 0\] = \(HEAPU8\[\(\$[0-9]+_1 \+ 3 \| 0\) >> 0\] \| 0\) \^ \(Math_imul\(\$[0-9]+_1, 27\) \+ 1 \| 0\) \| 0;");
                    var startidk = text.LastIndexOf("Math_imul", match2[match2.Count - 2].Index);
                    var idk = text.LastIndexOf("}", startidk);
                    var headerSubstitutionCount_text = text.Substring(idk, startidk - idk);
                    try
                    {
                        return ExtractClass(headerSubstitutionCount_text, "", 0);
                    }
                    catch
                    {
                        var labelstart = text.LastIndexOf(" : {", idk);
                        var ifstart = text.LastIndexOf("if ((", labelstart);
                        headerSubstitutionCount_text = text.Substring(ifstart, labelstart - ifstart);
                        return ExtractClass(headerSubstitutionCount_text, "", 0);
                    }
                }
            }
            var regex4 = new Regex(@"[$0-9_]+ = ([$0-9_]+) & 15 \| 0;");
            var divide = functionstring.LastIndexOf(" & 15 | 0;", tablematch);
            var match4 = regex4.Match(functionstring.Substring(divide - 50, 200));
            var variablename = match4.Groups[1].Value;
            if (divide < 0)
            {
                Debugger.Break();
            }
            save.s_headerSubstitutionCount = ExtractHeaderSubstitutionCount(variablename, functionstring.Substring(0, divide - 0));

            var whileloop = functionstring.LastIndexOf("while", tablematch);// - 500);
            //var b = functionstring.LastIndexOf("if", tablematch);
            //var whileloop = a < b ? b : a;
            var end = functionstring.LastIndexOf("};", whileloop);
            var HEAP8_1 = functionstring.IndexOf("HEAP8", end);
            var HEAP8_2 = functionstring.IndexOf("HEAP8", HEAP8_1 + 1);
            var endline = functionstring.IndexOf("\n", HEAP8_2);
            var xorTableGenerator_text = functionstring.Substring(HEAP8_1, endline - HEAP8_1);
            save.s_xorTableGenerator = ExtractClass(xorTableGenerator_text);

            var xorTableShuffler_text = functionstring.Substring(whileloop, tablematch - whileloop);
            save.s_xorTableShuffler = ExtractClass(xorTableShuffler_text);

            regex4 = new Regex(@" >>> 0\) % \(([0-9]+) >>> 0\) \| 0\) \| 0\) >> 0] \| 0\) \| 0;");
            match4 = regex4.Match(functionstring);
            save.s_xorTableSize = int.Parse(match4.Groups[1].Value);

            var cbox_match = new Regex(@"\$[0-9_]+ = 128;\r\n[ ]+\$[0-9_]+ = 127;").Match(all);
            if (cbox_match.Index == 0)
                cbox_match = new Regex(@"}\r\n[ ]+\$[0-9]+_[0-9]+ = \$[0-9]+;\r\n[ ]+HEAP32\[\(\$[0-9]+ \+ \d+ \| 0\) >> 2\] = HEAPU8\[\$[0-9]+_[0-9]+ >> 0\] \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 1 \| 0\) >> 0\] \| 0\) << 8 \| 0\) \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 2 \| 0\) >> 0\] \| 0\) << 16 \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 3 \| 0\) >> 0\] \| 0\) << 24 \| 0\) \| 0\) \| 0;").Match(all, 0, start);
            if (cbox_match.Index == 0)
                cbox_match = new Regex(@"}\r\n[ ]+\$[0-9]+_[0-9]+ = \$[0-9]+;\r\n[ ]+HEAP32\[\(\$[0-9]+ \+ \d+ \| 0\) >> 2\] = HEAPU8\[\$[0-9]+_[0-9]+ >> 0\] \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 1 \| 0\) >> 0\] \| 0\) << 8 \| 0\) \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 2 \| 0\) >> 0\] \| 0\) << 16 \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 3 \| 0\) >> 0\] \| 0\) << 24 \| 0\) \| 0\) \| 0;").Match(all, funcend);
            index = cbox_match.Index;
            //if (match.Count == 2)
            //    index = match[1].Index;
            //index = Regex.Match(all, @" >>> 0\) % \(([0-9]+) >>> 0\) \| 0\) \| 0\) >> 0] \| 0\) \| 0;", RegexOptions.RightToLeft).Index;
            //index = all.LastIndexOf("while", index);
            //if (match.Count == 3)
            //    index = match[1].Index;
            //else if (mmatch2.Count > 0)
            //    index = mmatch2[0].Index;
            start = all.LastIndexOf("function $", index);
            funcend = all.IndexOf("function $", index);
            functionstring = all.Substring(start, funcend - start);
            tablematch = regex.Match(functionstring).Index;
            skip = functionstring.IndexOf("label", 0) - 0;
            regex4 = new Regex(@"[$0-9_]+ = ([$0-9_]+) & 15 \| 0;");
            match4 = regex4.Match(functionstring);
            variablename = match4.Groups[1].Value;
            save.c_headerSubstitutionCount = ExtractHeaderSubstitutionCount(variablename, functionstring.Substring(0, match4.Index - 0));

            //if (match.Count <= 3)
            whileloop = functionstring.LastIndexOf("while", tablematch);
            //else
            //    whileloop = all.LastIndexOf("if", index);
            end = functionstring.LastIndexOf("};", whileloop);
            HEAP8_1 = functionstring.IndexOf("HEAP8", end);
            //HEAP8_2 = all.IndexOf("HEAP8", HEAP8_1 + 1);
            HEAP8_2 = functionstring.IndexOf("HEAP8", functionstring.IndexOf("HEAP8", HEAP8_1 + 1) + 1);
            xorTableGenerator_text = functionstring.Substring(HEAP8_1, HEAP8_2 - HEAP8_1);
            save.c_xorTableGenerator = ExtractClass(xorTableGenerator_text, "", 4);

            xorTableShuffler_text = functionstring.Substring(whileloop, tablematch - whileloop);
            save.c_xorTableShuffler = ExtractClass(xorTableShuffler_text);

            regex4 = new Regex(@" >>> 0\) % \(([0-9]+) >>> 0\) \| 0\) \| 0\) >> 0] \| 0\) \| 0;");
            match4 = regex4.Match(functionstring.Substring(tablematch, 2000));
            if (match4.Success)
                save.c_xorTableSize = int.Parse(match4.Groups[1].Value);
            else
                save.c_xorTableSize = save.s_xorTableSize;

            var sbox_text = all.Substring(all.IndexOf("while", sbox_match4.Index), all.IndexOf("label", all.IndexOf("while", sbox_match4.Index)) - all.IndexOf("while", sbox_match4.Index));
            sbox_seeds = all.Substring(all.LastIndexOf("};", sbox_match4.Index), all.IndexOf("while", sbox_match4.Index) - all.LastIndexOf("};", sbox_match4.Index));
            var temp = ExtractClass(sbox_text, "", 1);
            save.s_sBoxEncryptPRNG = Clone(temp);
            var sBoxEncrypt = new byte[128];
            for (byte i = 0; i < sBoxEncrypt.Length; i++)
            {
                sBoxEncrypt[i] = i;
            }
            for (var i = 127; i > 0; i--)
            {
                var idk3 = (uint)(temp.next());
                var newIndex = (idk3 % (uint)i) + 1;
                var temp2 = sBoxEncrypt[newIndex];
                sBoxEncrypt[newIndex] = sBoxEncrypt[i];
                sBoxEncrypt[i] = temp2;
            }
            save.s_sBoxEncrypt = sBoxEncrypt;
            //temp
            //sbox_match4=new Regex(@"\$[0-9]+_1 = \$[0-9]+_1 >>> 0 < [0-9]+ >>> 0 \? \$[0-9]+_1 \+ [0-9]+ \| 0 : \$[0-9]+_1 \+ [0-9]+ \| 0;").Match(all);
            /*sbox_match4 = new Regex(@"\$[0-9_]+ = 128;\r\n[ ]+\$[0-9_]+ = 127;").Match(all);
            //var whileidk =all.IndexOf("while", sbox_match4.Index);
            if (!sbox_match4.Success)
            {
                sbox_match4 = new Regex(@"\$[0-9]+_[0-9]+ = \$[0-9]+;\r\n[ ]+\$[0-9]+_[0-9]+ = HEAPU8\[\$[0-9]+_[0-9]+ >> 0\] \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 1 \| 0\) >> 0\] \| 0\) << 8 \| 0\) \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 2 \| 0\) >> 0\] \| 0\) << 16 \| 0 \| \(\(HEAPU8\[\(\$[0-9]+_[0-9]+ \+ 3 \| 0\) >> 0\] \| 0\) << 24 \| 0\) \| 0\) \| 0;\r\n[ ]+\$[0-9]+ = \$[0-9]+_[0-9]+;").Match(all);
                var whileidk = all.LastIndexOf("while", sbox_match4.Index);
                sbox_text = all.Substring(whileidk, all.IndexOf("label$", whileidk) - whileidk);
            }
            else
            {
                var whileidk = all.IndexOf("while", sbox_match4.Index);
                sbox_text = all.Substring(whileidk, all.IndexOf("label$", whileidk) - whileidk);
            }*/
            //sbox_index = all.LastIndexOf("while", sbox_match4.Index);
            sbox_match4 = cbox_match;
            var whileidk = all.IndexOf("while", sbox_match4.Index);
            sbox_text = all.Substring(whileidk, all.IndexOf("label$", whileidk) - whileidk);
            /*var whileidk = all.LastIndexOf("while", sbox_match4.Index);
            sbox_text = all.Substring(whileidk, all.IndexOf("label$", whileidk) - whileidk);
            if (sbox_text.Length < 50)
                sbox_text = all.Substring(whileidk, all.IndexOf("}", all.IndexOf("}", whileidk)) - whileidk);*/
            sbox_seeds = functionstring; //all.Substring(all.LastIndexOf("};", sbox_match4.Index), all.IndexOf("while", sbox_match4.Index) - all.LastIndexOf("};", sbox_match4.Index));
            temp = ExtractClass(sbox_text, "", 1);
            save.c_sBoxDecryptPRNG = Clone(temp);
            var sBoxDecrypt = new byte[128];
            for (byte i = 0; i < 128; i++)
            {
                sBoxDecrypt[i] = i;
            }
            for (var i = 127; i > 0; i--)
            {
                var idk3 = (uint)(temp.next());
                var newIndex = (idk3 % (i + 1));
                var temp2 = sBoxDecrypt[newIndex];
                sBoxDecrypt[newIndex] = sBoxDecrypt[i];
                sBoxDecrypt[i] = temp2;
            }
            sBoxDecrypt[sBoxDecrypt.ToList().IndexOf(1)] = sBoxDecrypt[1];
            sBoxDecrypt[1] = 1;
            var sBoxDecryptlist = sBoxDecrypt.ToList();
            sBoxDecrypt = sBoxDecryptlist.Select((n, l) => (byte)sBoxDecryptlist.IndexOf((byte)l)).ToArray();
            save.c_sBoxDecrypt = sBoxDecrypt;
            //save.c_sBoxDecrypt= new byte[]{123,1,32,22,71,26,84,3,54,30,105,20,103,7,52,13,99,60,98,34,111,18,80,94,64,5,4,11,114,50,2,112,93,24,12,21,125,72,42,106,127,35,58,56,88,19,104,31,73,10,0,82,63,6,97,47,78,33,14,9,70,90,40,59,124,48,44,96,79,66,113,116,91,28,126,74,107,23,89,92,109,27,41,75,53,100,118,81,67,46,51,85,101,69,15,55,17,120,65,110,83,68,57,39,77,76,36,62,119,37,49,95,16,86,25,102,117,108,121,115,122,38,45,61,43,87,8,29 };
            //}
            TextWriter writer = null;
            try
            {
                File.WriteAllText(INFO.headless_config_file, JsonConvert.SerializeObject(save, Formatting.Indented));
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
        public static PRNG Clone<T>(T classToClone)
        {
            var type = classToClone.GetType();
            return (PRNG)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(classToClone), type);
        }
        static string GetBuild()
        {
            string data = GetData("https://diep.io/");//await tor.GetString("https://diep.io/");
            if (string.IsNullOrWhiteSpace(data))
                return null;
            Regex regex = new Regex("script defer=\"defer\" src=\"\\/([.js?0-9a-z]+)\"");
            Match match = regex.Match(data);
            if (!match.Success)
                return null;
            data = GetData(Path.Combine("https://diep.io/", match.Groups[1].Value));
            regex = new Regex("\\+\"([0-9a-f]{20})\\.wasm\"");
            match = regex.Match(data);
            if (!match.Success)
                return null;
            string build = match.Groups[1].Value;
            var wasm_path = Path.Combine(currentfolder, build + ".wasm");
            var js_path = Path.Combine(currentfolder, build + ".js");
            if (File.Exists(wasm_path) && File.Exists(js_path))
                return build;
            var wasm2js = Path.Combine(currentfolder, "wasm2js.exe");
            File.WriteAllBytes(wasm_path, GetDatabytes(Path.Combine("https://diep.io/", build + ".wasm")));
            ProcessStartInfo startInfo2 = new ProcessStartInfo
            {
                FileName = (wasm2js),
                Arguments = $"\"{wasm_path}\" -o \"{js_path}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            var p = Process.Start(startInfo2);
            p.WaitForExit();
            int count = 0;
            while (!File.Exists(js_path) && count < 300)
            {
                Thread.Sleep(100);
                count++;
            }
            return build;
        }
        static string GetData(string url)
        {
            HttpClient client = new HttpClient();
            return client.GetAsync(url).Result.Content.ReadAsStringAsync().Result;
        }
        static byte[] GetDatabytes(string url)
        {
            HttpClient client = new HttpClient();
            return client.GetAsync(url).Result.Content.ReadAsByteArrayAsync().Result;
        }
        public class CipherSave
        {
            public int s_xorTableSize;
            public PRNG s_headerSubstitutionCount;
            public PRNG s_xorTableGenerator;
            public PRNG s_xorTableShuffler;
            public PRNG s_sBoxEncryptPRNG;
            public byte[] s_sBoxEncrypt = new byte[128];
            public int c_xorTableSize;
            public PRNG c_headerSubstitutionCount;
            public PRNG c_xorTableGenerator;
            public PRNG c_xorTableShuffler;
            public PRNG c_sBoxDecryptPRNG;
            public byte[] c_sBoxDecrypt = new byte[128];
        }
    }
}
