using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace DiepClient
{

    #region js
    public struct entid_class
    {
        public UInt64 hash;
        public UInt64 id;
        public override string ToString()
        {
            return "hash = " + hash + " id = " + id;
        }
    }
    public struct table_class
    {
        public int index;
        public string name;
        public object val;
    }
    public class Reader
    {
        public int at = 0;
        public byte[] buffer;
        public bool canread()
        {
            return buffer.Length > at;
        }
        public byte[] getbytes(int length)
        {
            var bytes = buffer.Skip(at).Take(length);
            at += length;
            return bytes.ToArray();
        }
        public byte parse_u8()
        {
            return buffer[at++];
        }
        public ushort parse_u16()
        {
            var num = BitConverter.ToUInt16(buffer, at);
            at += 2;
            return num;
        }
        public ushort parse_E_u16()
        {
            var num = BitConverter.ToUInt16(buffer, at);
            at += 2;
            return num;
        }
        public int parse_i32()
        {
            var num = BitConverter.ToInt32(buffer, at);
            at += 4;
            return num;
        }
        public uint parse_u32()
        {
            var num = BitConverter.ToUInt32(buffer, at);
            at += 4;
            return num;
        }
        public long parse_vi()
        {
            var output = this.parse_vu();
            var sign = output & 1;
            output >>= 1;
            long output2 = (long)output;
            if (sign != 0) output2 = (long)~output;
            //Debug.WriteLine(output + " " + (long)((0 - (output & 1)) ^ (ulong)((long)((ulong)output >> 1))));
            return output2;
        }
        public float parse_float()
        {
            float output = BitConverter.ToSingle(buffer, at);
            at += 4;
            return output;
        }
        public ulong parse_vu()
        {
            ulong result = 0;
            int shift = 0;
            for (; ; )
            {
                result |= (buffer[at] & 0x7Fu) << shift;
                if ((buffer[at] & 0x80) == 0)
                    break;
                shift += 7;
                at++;
            }
            at++;

            //if (endianSwap) output = Reader.endianSwap(output);

            return result;
        }
        public ulong parse_flags()
        {
            return parse_vu();
        }
        public ulong parse_color()
        {
            return parse_vu();
        }
        public long parse_tank()
        {
            return parse_vi();
        }
        public string parse_cstr()
        {
            var end = Array.IndexOf(buffer, (byte)0, this.at) - this.at;

            var output = Encoding.UTF8.GetString(buffer.Skip(this.at).Take(end).ToArray());
            this.at += end + 1;
            return output;
        }
        public entid_class? parse_entid()
        {
            if (this.buffer[this.at] == 0)
            {
                var value = ((this.at++) & 0);
                if (value == 0)
                {
                    return null;
                }
                return null;
            }
            return new entid_class()
            {
                hash = this.parse_vu(),
                id = this.parse_vu()
            };
        }
        uint Swap(uint val)
        {
            // Swap adjacent 16-bit blocks
            val = (val >> 16) | (val << 16);
            // Swap adjacent 8-bit blocks
            val = ((val & 0xFF00FF00U) >> 8) | ((val & 0x00FF00FFU) << 8);
            return val;
        }
        int Swap(long val)
        {
            unchecked
            {
                return (int)Swap((uint)val);
            }
        }
        public float parse_vf()
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(Swap(parse_vi())), 0);
        }
    }
    public class Decompress : Reader
    {
        public byte[] DecompressPacket(byte[] buf)
        {
            buffer = buf;
            //console.log(this.at);
            var outSize = this.parse_u32();
            var decompressed = new byte[outSize];
            //console.log(bytes, outSize, this.at);
            //var pos = this.at;
            var outPos = 0;
            while (at < buffer.Length)
            {
                var token = this.parse_u8();//view.getUint8(pos++);
                var literalLength = token >> 4;
                var matchLength = token & 0xF;
                if (literalLength == 0xF)
                {
                    do literalLength += buffer[at];
                    while (this.parse_u8() == 0xFF);
                }
                Buffer.BlockCopy(buffer, at, decompressed, outPos, literalLength);
                at += literalLength;
                outPos += literalLength;
                //Debug.WriteLine(outPos);
                //for (var i = 0; i < literalLength; i++)
                //{
                //console.log(outPos,pos);
                //decompressed[outPos++] = parse_u8();
                //}

                if (at == buffer.Length) break;
                var offset = parse_E_u16();//view.getUint16(pos, true);
                //if (offset != offset2) throw new Exception("Invalid offset");
                if (offset == 0) throw new Exception("Invalid offset");
                //if (outPos >= 14900)Debugger.Break();
                if (matchLength == 0xF)
                {
                    do matchLength += buffer[at];
                    while (this.parse_u8() == 0xFF);
                }
                matchLength += 4;
                for (var i = 0; i < matchLength; i++)
                    //{
                    //console.log(offset, outPos + i, outPos - offset + i % offset);
                    decompressed[outPos + i] = decompressed[outPos - offset + i % offset];
                //decompressed.setUint8(outPos + i, decompressed.getUint8(outPos - offset + i % offset));
                //}
                outPos += matchLength;
                //Debug.WriteLine(outPos);
                if (at > buffer.Length) throw new Exception("Passed end");
            }
            if (outPos != outSize) throw new Exception("Wrong length " + outPos + " " + outSize);
            return decompressed;
        }
    }
    public class Parser : Reader
    {
        public ulong uptick = 0;
        public bool failed;
        public ConcurrentDictionary<ulong, Object_class> all = new ConcurrentDictionary<ulong, Object_class>();
        public INFO info;
        public leaderboard leaderboard = null;

        //public StringBuilder debugstream = new StringBuilder();
        public StreamWriter debugstream;
        public ulong scoreboardAmount;
        public string[] scoreboardNames = new string[10];
        public float[] scoreboardScores = new float[10];
        public ulong[] scoreboardColors = new ulong[10];//color_class
        public string[] scoreboardSuffixes = new string[10];
        public long[] scoreboardTanks = new long[10];
        public string[] playerids;
        public string[] playernames;
        public ulong playercount;
        public float arenaLeftX;
        public float arenaRightX;
        public float arenaTopY;
        public float arenaBottomY;
        public float FOV;
        public float cameraX;
        public float cameraY;
        public float leaderX;
        public float leaderY;
        public ulong? GUI_id;
        public ulong? player_id;
        public static int count = 0;
        public static int paccount = 0;
        public Parser(INFO info, bool debug = false)
        {
            this.info = info;
            if (debug)
                debugstream = new StreamWriter(File.Create("test" + (count++).ToString() + ".txt"));
        }
        public ulong? parse(byte[] buf)
        {
            //File.WriteAllBytes(2 + "-" + paccount++ + ".bin", buf);
            failed = false;
            buffer = buf;
            try
            {
                if (buffer[at] != 0x00) { Debug.WriteLine("at wrong index " + at); return (ulong?)at; }
                at++;
                uptick = parseUptick();

                //Debug.WriteLine("uptick " + uptick);
                ulong delCount = parseDelCount();
                //Debug.WriteLine("delCount " + delCount);
                for (ulong i = 0; i < delCount; i++)
                {
                    entid_class? entid = this.parse_entid();

                    //var index = all.FindIndex(x => x.id == entid.Value.id);
                    //if (index != -1)
                    //if (!
                    if (all.TryRemove(entid.Value.id, out Object_class deleted))
                        if (deleted != null)
                            deleted.Dispose();
                        else
                            Debug.WriteLine("delete index==-1" + " " + entid);
                    else
                        Debug.WriteLine("delete index==-1" + " " + entid);
                    //HeadlessMain.Objects.TryRemove((int)entid.Value.id, out _);
                    //    ) Debug.WriteLine("delete index==-1" + " " + entid);
                    //else
                    //{
                    //this.failed=true;
                    //Debug.WriteLine("delete index==-1" + " " + entid);
                    //return this.failed;
                    //}
                }
                ulong updCount = parseUpdCount();
                //Debug.WriteLine("updCount " + updCount);
                //Debug.WriteLine("current index: " + at);
                //console.log("idk4.5 " + updCount + " " + this.at);
                if (updCount > (ulong)buffer.Length)
                {
                    //MessageBox.Show("updCount too big " + updCount);
                    return (ulong?)at;
                }
                //Debug.WriteLine("at: " + at);
                for (ulong i = 0; i < updCount + 1; i++)
                {
                    try
                    {
                        ParseUpdate();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        try
                        {
                            File.WriteAllBytes("buffer.buf", buffer);
                        }
                        catch { }
                        Crash();
                        Debugger.Break();
                        System.Environment.Exit(0);
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                StringCipher.LogError("parseError.txt", ex.ToString());
            }
            count++;
            //Debug.WriteLine(string.Join(",",buffer.Skip(at).ToArray()));
            return (ulong?)at;
        }

        public UInt64 parseUptick()
        {
            UInt64 uptimeXor = info.UPTIME_XOR;
            if (uptimeXor == 0) return parse_vu();
            return (parse_vu() ^ uptimeXor);
            //if (typeof uptimeXor === 'function') return uptimeXor(this.vu());

            //return this.vu();

        }
        public UInt64 parseDelCount()
        {
            UInt64 delCountXor = info.DEL_COUNT_XOR;
            if (delCountXor == 0) return parse_vu();
            return parse_vu() ^ ((uptick + delCountXor) & 127);
            //if (typeof delCountXor === 'function') return delCountXor(this.vu(), this.uptick);

            //return this.vu();

        }
        public UInt64 parseUpdCount()
        {
            UInt64 updCountXor = info.UPD_COUNT_XOR;
            if (updCountXor == 0) return parse_vu();
            return parse_vu() ^ ((uptick + updCountXor) & 127);
            //if (typeof updCountXor === 'function') return updCountXor(this.vu(), this.uptick);

            //return this.vu();

        }
        void DebugWriteLine(string str)
        {
            if (debugstream != null)
                debugstream.WriteLine(str);
        }
        void Crash()
        {
            if (debugstream != null)
                debugstream.Close();
            File.WriteAllBytes("crash", buffer);
        }
        bool ParseUpdate()
        {
            //Debug.WriteLine("start " + at);
            DebugWriteLine("start " + at);
            entid_class? id = this.parse_entid();
            byte type = this.parse_u8();
            //buffer
            //Debug.WriteLine(id + " type: " + type);
            //Debug.WriteLine("at: " + at);
            DebugWriteLine(id + " type: " + type);
            if (type == 0)
            { // UPDATE
                this.at++;
                var isinlist = all.ContainsKey(id.Value.id);
                //if (!isinlist) Debug.WriteLine("update index==-1" + " " + id.Value.id);
                Object_class current = null;

                if (isinlist)
                    current = all[id.Value.id];
                //var index = all.FindIndex(x => x.id == id.Value.id);
                //if (index == -1)
                //    Debug.WriteLine("update index==-1" + " " + id.Value.id);
                int jumpindex = -1;
                int currentJump = 0;
                while (true)
                {
                    currentJump = (int)parse_vu() ^ 1;
                    if (currentJump == 0) break;
                    jumpindex += currentJump;
                    if (jumpindex > info.FIELD_ORDER.Length)
                    {
                        Crash();
                        MessageBox.Show("jumpindex > info.FIELD_ORDER.Length");
                    }
                    var fieldName = info.FIELD_ORDER[jumpindex].name;
                    var fieldType = info.FIELD_ORDER[jumpindex].type;
                    DebugWriteLine("fieldName: " + fieldName + " type: " + fieldType);
                    if (fieldType.EndsWith("[]"))
                    {
                        if (fieldName == "scoreboardScores")
                            fieldType = "long";
                        fieldType = fieldType.Replace("[]", "");

                        int jumpindex2 = -1;
                        int currentJump2 = 0;
                        Array field_array = null;
                        if (isinlist)
                        {
                            field_array = GetArray(current, fieldName);
                        }
                        while (true)
                        {
                            currentJump2 = (int)parse_vu() ^ 1;
                            if (currentJump2 == 0) break;
                            jumpindex2 += currentJump2;
                            object value = ParseValue(fieldType);
                            //if (value != null)
                            //    Debug.WriteLine("fieldValue: " + value + " end_at: " + at);
                            //else
                            //    Debug.WriteLine("fieldValue: " + "null " + "end_at: " + at);
                            if (value != null)
                                DebugWriteLine("fieldValue: " + value + " end_at: " + at);
                            else
                                DebugWriteLine("fieldValue: " + "null " + "end_at: " + at);
                            if (isinlist && field_array != null)
                            {
                                field_array.SetValue(value, jumpindex2);
                            }
                        }

                        if (isinlist && field_array != null)
                        {
                            UpdateValue(current, fieldName, field_array);
                        }
                    }
                    else
                    {
                        var value = ParseValue(fieldType);
                        //if (value != null)
                        //    Debug.WriteLine("fieldValue: " + value + " end_at: " + at);
                        //else
                        //    Debug.WriteLine("fieldValue: " + "null " + "end_at: " + at);
                        if (value != null)
                            DebugWriteLine("fieldValue: " + value + " end_at: " + at);
                        else
                            DebugWriteLine("fieldValue: " + "null " + "end_at: " + at);
                        if (isinlist)
                            UpdateValue(current, fieldName, value);
                    }
                }
            }
            else if (type == 1)
            { // CREATION
                List<int> table = new List<int>();

                int index = -1;
                int currentJump = 0;
                int oldat = at;
                while (true)
                {
                    currentJump = (int)parse_vu() ^ 1;
                    if (currentJump == 0) break;
                    index += currentJump;
                    table.Add(index);
                }
                if (table.Count == 0)
                    throw new Exception("table.Count == 0");
                //Debug.WriteLine(string.Join(",", table) + " at: " + oldat);
                table = table.Select(x => info.swapkey[x]).ToList();
                //Debug.WriteLine(string.Join(",", table) + " at: " + oldat);
                DebugWriteLine(string.Join(",", table) + " at: " + oldat);
                INFO.new_field[] fields;
                try
                {
                    fields = info.sortedgroups[table];
                }
                catch
                {
                    Debug.WriteLine("table " + string.Join(", ", table));
                    info.GenerateSortedGroup(table);
                    fields = info.sortedgroups[table];
                }
                table_class[] data = new table_class[fields.Length];
                var fieldgroups = new bool[17];
                for (int i = 0; i < table.Count; i++)
                {
                    fieldgroups[table[i]] = true;
                }
                Object_class Object = new Object_class
                {
                    fieldgroups = fieldgroups,
                    id = id.Value.id
                };
                for (int i = 0; i < fields.Length; i++)
                {
                    var fieldType = "";
                    try
                    {
                        fieldType = fields[i].type;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        StringCipher.LogError("parseError2.txt", ex.ToString());
                        Debugger.Break();
                    }
                    //Debug.WriteLine("fieldName: " + fields[i].name + " type: " + fieldType + " isWonky: " + (fieldType[0] == '_') + " at: " + at);
                    DebugWriteLine("fieldName: " + fields[i].name + " type: " + fieldType + " isWonky: " + (fieldType[0] == '_') + " at: " + at);
                    if (fieldType.EndsWith("[]"))
                    {
                        var correcttype = fieldType.Replace("[]", "");
                        int amount = 0;
                        if (fields[i].fieldGroup == FIELDS.PlayerInfo)
                            amount = 100;
                        else if (fields[i].fieldGroup == FIELDS.Arena)
                            amount = 10;
                        else if (fields[i].fieldGroup == FIELDS.GUI)
                            amount = 8;
                        else
                            amount = FIELDS.amount[fields[i].name];
                        Type arraytype = null;
                        switch (correcttype)
                        {
                            case "ulong": arraytype = typeof(ulong); break;
                            case "long": arraytype = typeof(long); break;
                            case "float": arraytype = typeof(float); break;
                            case "string": arraytype = typeof(string); break;
                        }
                        Array arr = Array.CreateInstance(arraytype, amount);
                        string[] stringarray = new string[amount];
                        for (var i2 = 0; i2 < amount; i2++)
                        {
                            arr.SetValue(ParseValue(correcttype), i2);
                            stringarray[i2] = arr.GetValue(i2).ToString();
                        }
                        DebugWriteLine(" " + fields[i].type + " " + string.Join(", ", stringarray) + " end_at: " + at);
                        //Debug.WriteLine(" " + fields[i].type + " " + string.Join(", ", stringarray) + " end_at: " + at);
                        data[i] = new table_class() { name = fields[i].name, val = arr };
                        UpdateValue(Object, fields[i].name, arr);
                    }
                    else
                    {
                        object idk = ParseValue(fieldType);
                        //if (idk != null)
                        //    Debug.WriteLine(" fieldValue: " + idk.ToString() + " end_at: " + at);
                        //else
                        //    Debug.WriteLine(" fieldValue: " + "null " + "end_at: " + at);
                        if (idk != null)
                            DebugWriteLine(" fieldValue: " + idk.ToString() + " end_at: " + at);
                        else
                            DebugWriteLine(" fieldValue: " + "null " + "end_at: " + at);
                        if (fields[i].name == "x")
                            Object.x = (long)idk;
                        else if (fields[i].name == "y")
                            Object.y = (long)idk;
                        UpdateValue(Object, fields[i].name, idk);
                    }
                }
                //Object.children.Reverse();
                if (!all.TryAdd(id.Value.id, Object))
                {
                    all[id.Value.id].Dispose();
                    all.TryAdd(id.Value.id, Object);
                }
            }
            else
            {
                Debug.WriteLine("Error in processing of update : " + (id.HasValue ? id.Value.id + "" : "") + " " + type + " " + this.at);
                //MessageBox.Show("Error in processing of update : " + id.Value.id + " " + type + " " + this.at);
                Crash();
                Debugger.Break();
                return false;
            }
            return true;
        }
        object ParseValue(string fieldType)
        {
            switch (fieldType)
            {
                case "ulong":
                    return parse_vu();
                case "float":
                    return parse_float();
                case "long":
                    return parse_vi();
                case "entid":
                    return parse_entid();
                case "string":
                    return parse_cstr();
                //case "flags":
                //    return parse_flags();
                //case "tank":
                //    return parse_tank();
                //case "color":
                //    return parse_color();
                default:
                    Debug.WriteLine("value" + fieldType);
                    return null;
            }
        }
        public Array GetArray(Object_class Object, string name)
        {
            switch (name)
            {
                case "scoreboardNames":
                    return Object.scoreboardNames;
                case "scoreboardScores":
                    return Object.scoreboardScores;
                case "scoreboardColors":
                    return Object.scoreboardColors;
                case "scoreboardSuffixes":
                    return Object.scoreboardSuffixes;
                case "scoreboardTanks":
                    return Object.scoreboardTanks;
                case "statNames":
                    return Object.statNames;
                case "statLevels":
                    return Object.statLevels;
                case "statMaxes":
                    return Object.statMaxes;
                case "playernames":
                    return Object.playernames;
                case "playerids":
                    return Object.playerids;
            }
            return null;
        }
        Dictionary<ulong, string> previous = new Dictionary<ulong, string>();
        public void UpdateValue(Object_class Object, string name, object value)
        {
            switch (name)
            {
                case "parent":
                    {
                        if (value != null)
                        {
                            Object.parent = (entid_class?)value;

                            //var temp = all.Find(x => x.id == ((entid_class)value).id);
                            if (all.ContainsKey(Object.parent.Value.id))
                            {
                                lock (all[Object.parent.Value.id].children)
                                {
                                    all[Object.parent.Value.id].children.Add(Object.id);
                                    all[Object.parent.Value.id].children.OrderBy(x => all[Object.parent.Value.id].serverEntityCount);
                                }
                            }
                            //if (all[Object.parent.Value.id].previous_hp != -1f)
                            //    all[Object.parent.Value.id].children.Add(Object.id);
                            //else
                            //    all[Object.parent.Value.id].children.Insert(0, Object.id);
                        }
                        else
                            Object.parent = null;
                        break;
                    }
                case "owner":
                    if (value != null)
                        Object.owner = (entid_class)value;
                    else
                        Object.owner = null;
                    break;
                case "team":
                    if (value != null)
                        Object.team = (entid_class)value;
                    else
                        Object.team = null;
                    break;
                case "shooting":
                    Object.shooting = (ulong)value;
                    break;
                case "reloadTime":
                    Object.reloadTime = (float)value;
                    Debug.WriteLine((float)value);
                    break;
                case "shootingAngle":
                    Object.shootingAngle = (float)value;
                    break;
                case "Object":
                    Object.Object = (ulong)value;
                    break;
                case "sides":
                    Object.sides = (ulong)value;
                    break;
                case "size":
                    Object.size = (float)value;
                    break;
                case "width":
                    Object.width = (float)value;
                    break;
                case "knockbackFactor":
                    Object.knockbackFactor = (float)value;
                    break;
                case "damageFactor":
                    Object.damageFactor = (float)value;
                    break;
                case "healthbar":
                    Object.healthbar = (ulong)value;
                    break;
                case "health":
                    Object.previous_hp = Object.health;
                    Object.health = (float)value;
                    break;
                case "maxHealth":
                    Object.maxHealth = (float)value;
                    break;
                case "unknown":
                    Object.unknown = (long)value;
                    break;
                case "playercount":
                    Object.playercount = (ulong)value;
                    playercount = Object.playercount;
                    if (leaderboard != null)
                    {
                        leaderboard.playercount = (int)playercount;
                    }
                    //Debug.WriteLine("playercount" + " " + Object.playercount);
                    break;
                case "playernames":
                    Object.playernames = (string[])value;
                    playernames = Object.playernames;
                    //Debug.WriteLine("playernames" + " " + string.Join(" ", playernames));
                    break;
                case "playerids":
                    Object.playerids = (string[])value;
                    playerids = Object.playerids;
                    //Debug.WriteLine("playerids" + " " + string.Join(" ", playerids));
                    break;
                case "GUI":
                    Object.GUI = (ulong)value;
                    break;
                case "arenaLeftX":
                    if ((float)value > 0)
                    {
                        arenaLeftX = -(float)value;
                        arenaTopY = -(float)value;
                        arenaRightX = (float)value;
                        arenaBottomY = (float)value;
                    }
                    else
                    {
                        arenaLeftX = (float)value;
                        arenaTopY = (float)value;
                        arenaRightX = -(float)value;
                        arenaBottomY = -(float)value;
                    }
                    //Object.arenaLeftX = (float)value;
                    //arenaLeftX = (float)value;
                    break;
                case "arenaTopY":
                    Object.arenaTopY = (float)value;
                    arenaTopY = (float)value;
                    break;
                case "arenaRightX":
                    Object.arenaRightX = (float)value;
                    arenaRightX = (float)value;
                    break;
                case "arenaBottomY":
                    Object.arenaBottomY = (float)value;
                    arenaBottomY = (float)value;
                    break;
                case "scoreboardAmount":
                    Object.scoreboardAmount = (ulong)value;
                    scoreboardAmount = Object.scoreboardAmount;
                    if (leaderboard != null)
                    {
                        leaderboard.lb.lbamount = (int)scoreboardAmount;
                    }
                    break;
                case "scoreboardNames":
                    Object.scoreboardNames = (string[])value;
                    scoreboardNames = Object.scoreboardNames;
                    if (leaderboard != null)
                    {
                        leaderboard.lb.name = scoreboardNames;
                    }
                    break;
                case "scoreboardScores":
                    Object.scoreboardScores = (float[])value;
                    scoreboardScores = Object.scoreboardScores;
                    if (leaderboard != null)
                    {
                        leaderboard.lb.score = scoreboardScores;
                    }
                    break;
                case "scoreboardColors":
                    Object.scoreboardColors = (ulong[])value;
                    scoreboardColors = Object.scoreboardColors;
                    if (leaderboard != null)
                    {
                        leaderboard.lb.color = scoreboardColors;
                    }
                    break;
                case "scoreboardSuffixes":
                    Object.scoreboardSuffixes = (string[])value;
                    scoreboardSuffixes = Object.scoreboardSuffixes;
                    break;
                case "scoreboardTanks":
                    Object.scoreboardTanks = (long[])value;
                    scoreboardTanks = Object.scoreboardTanks;
                    if (leaderboard != null)
                    {
                        leaderboard.lb.tank = scoreboardTanks;
                    }
                    break;
                case "leaderX":
                    Object.leaderX = (float)value;
                    leaderX = Object.leaderX;
                    break;
                case "leaderY":
                    Object.leaderY = (float)value;
                    leaderY = Object.leaderY;
                    break;
                case "playersNeeded":
                    Object.playersNeeded = (long)value;
                    break;
                case "ticksUntilStart":
                    Object.ticksUntilStart = (float)value;
                    break;
                case "nametag":
                    Object.nametag = (ulong)value;
                    break;
                case "name":
                    Object.name = (string)value;
                    break;
                case "GUIunknown":
                    Object.GUIunknown = (ulong)value;
                    break;
                case "camera":
                    Object.camera = (ulong)value;
                    break;
                case "player":
                    GUI_id = Object.id;
                    if (value != null)
                    {
                        Object.player = (entid_class)value;
                        lock (HeadlessMain.mytanks)
                            if (player_id != null && HeadlessMain.mytanks.Contains(player_id.Value))
                                HeadlessMain.mytanks.Remove(player_id.Value);
                        player_id = Object.player.Value.id;
                        lock (HeadlessMain.mytanks)
                        {
                            if (player_id != null && HeadlessMain.mytanks.Contains(player_id.Value))
                                HeadlessMain.mytanks.Remove(player_id.Value);
                            HeadlessMain.mytanks.Add(Object.player.Value.id);
                        }
                    }
                    else
                    {
                        lock (HeadlessMain.mytanks)
                        {
                            if (player_id != null && HeadlessMain.mytanks.Contains(player_id.Value))
                                HeadlessMain.mytanks.Remove(player_id.Value);
                        }
                        player_id = null;
                    }
                    break;
                case "FOV":
                    FOV = (float)value;
                    Object.FOV = (float)value;
                    break;
                case "level":
                    Object.level = (long)value;
                    break;
                case "tank":
                    Object.tank = (long)value;
                    break;
                case "levelbarProgress":
                    Object.levelbarProgress = (float)value;
                    break;
                case "levelbarMax":
                    Object.levelbarMax = (float)value;
                    break;
                case "statsAvailable":
                    Object.statsAvailable = (long)value;
                    break;
                case "statNames":
                    Object.statNames = (string[])value;
                    break;
                case "statLevels":
                    Object.statLevels = (long[])value;
                    break;
                case "statMaxes":
                    Object.statMaxes = (long[])value;
                    break;
                case "cameraX":
                    Object.cameraX = (float)value;
                    cameraX = (float)value;
                    break;
                case "cameraY":
                    Object.cameraY = (float)value;
                    cameraY = (float)value;
                    break;
                case "scorebar":
                    Object.scorebar = (float)value;
                    break;
                case "respawnLevel":
                    Object.respawnLevel = (long)value;
                    break;
                case "killedByID":
                    Object.killedByID = (string)value;
                    //Debug.WriteLine("killedByID " + " " + Object.killedByID);
                    break;
                case "killedBy":
                    Object.killedBy = (string)value;
                    //Debug.WriteLine("killedBy " + " " + Object.killedBy);
                    break;
                case "spawnTick":
                    Object.spawnTick = (long)value;
                    break;
                case "deathTick":
                    Object.deathTick = (long)value;
                    break;
                case "tankOverride":
                    Object.tankOverride = (string)value;
                    break;
                case "movementSpeed":
                    Object.movementSpeed = (float)value;
                    break;
                case "x":
                    Object.previous_x = Object.x;
                    Object.x = (long)value;
                    break;
                case "y":
                    Object.previous_y = Object.y;
                    Object.y = (long)value;
                    break;
                case "angle":
                    Object.angle = (long)value;
                    break;
                case "motion":
                    Object.motion = (ulong)value;
                    break;
                case "style":
                    Object.style = (ulong)value;
                    break;
                case "color":
                    if ((ulong)value <= (ulong)INFO.COLORS_RGB.Length)
                        Object.color = (ulong)value;
                    break;
                case "borderThickness":
                    Object.borderThickness = (long)value;
                    break;
                case "opacity":
                    Object.opacity = (float)value;
                    break;
                case "serverEntityCount":
                    Object.serverEntityCount = (ulong)value;
                    break;
                case "score":
                    Object.score = (float)value;
                    break;
                case "teamColor":
                    Object.teamColor = (ulong)value;
                    break;
                case "mothershipX":
                    Object.mothershipX = (float)value;
                    break;
                case "mothershipY":
                    Object.mothershipY = (float)value;
                    break;
                case "mothership":
                    Object.mothership = (ulong)value;
                    break;
                default:
                    //if (value == null)
                    //    Debug.WriteLine("name: " + name + " " + null);
                    //else
                    //    Debug.WriteLine("name: " + name + " " + value.GetType().Name + " " + value.ToString());
                    break;
            }
        }
        public void Dispose()
        {
            if (debugstream != null)
                debugstream.Close();
            foreach (var value in all.Values)
                value.Dispose();
            if (leaderboard != null)
                lock (diep_directx.leaderboards)
                {
                    var index = diep_directx.leaderboards.FindIndex(x => x.server == leaderboard.server);
                    if (index != -1)
                        diep_directx.leaderboards.RemoveAt(index);
                    leaderboard.Dispose();
                }
        }
    }

    public class FIELDS
    {
        public static int Owner = 0, Score = 1, Physics = 2, Scores = 3, Barrel = 13, Name = 6, Arena = 7, Position = 8, GUI = 9, PlayerInfo = 10, Unknown = 11, Mothership = 12, Health = 5, Display = 14;
        public static Dictionary<string, int> amount = new Dictionary<string, int>()
        {
            {"scoreboardNames", 10},
            {"scoreboardScores", 10},
            {"scoreboardColors", 10},
            {"scoreboardSuffixes", 10},
            {"scoreboardTanks", 10},
            {"statNames", 8},
            {"statLevels", 8},
            {"statMaxes", 8},
            {"playernames", 80},
            {"playerdescriptions", 80}
        };
    }
    //public class flags_class
    //{
    //    public ulong value;
    //    public override string ToString()
    //    {
    //        return value.ToString();
    //    }
    //}
    public class Object_class : IDisposable
    {
        public bool[] fieldgroups = new bool[17];
        public List<ulong> children = new List<ulong>();
        public ulong id;
        public long previous_x;
        public long previous_y;
        public float previous_hp = -1;
        public bool disposed = false;
        public object textLayoutlock = new object();
        public SharpDX.DirectWrite.TextLayout nametextLayout;
        public SharpDX.DirectWrite.TextLayout scoretextLayout;
        public string previousscore;
        // 0
        public entid_class? parent = null;
        public entid_class? owner = null;
        public entid_class? team = null;
        // 2
        public ulong shooting;
        public float reloadTime;
        public float shootingAngle;
        // 3
        public ulong Object; //originally object
        public ulong sides;
        public float size;
        public float width;
        public float knockbackFactor;
        public float damageFactor;
        // 4
        public ulong healthbar;
        public float health = -1;
        public float maxHealth;
        // 6
        public long unknown;
        // 7
        public ulong GUI;
        public float arenaLeftX;
        public float arenaTopY;
        public float arenaRightX;
        public float arenaBottomY;
        public ulong scoreboardAmount;
        public string[] scoreboardNames;
        public ulong[] scoreboardColors;//color_class
        public string[] scoreboardSuffixes;
        public long[] scoreboardTanks;
        public long playersNeeded;
        public float ticksUntilStart;
        // 8
        public ulong nametag;
        public string name;
        // 9
        public ulong GUIunknown;
        public ulong camera;
        public entid_class? player = null;
        public float FOV;
        public long level;
        public long tank;
        public float levelbarProgress;
        public float levelbarMax;
        public long statsAvailable;
        public string[] statNames;
        public long[] statLevels;
        public long[] statMaxes;
        public float cameraX;
        public float cameraY;
        public float scorebar;
        public long respawnLevel;
        public string killedBy;
        public string killedByID;
        public long spawnTick;
        public long deathTick;
        public string tankOverride;
        public float movementSpeed;
        //10 Scores
        public float leaderX;
        public float leaderY;
        public float[] scoreboardScores;
        // 11
        public long x;
        public long y;
        public long angle;
        public ulong motion;
        // 12
        public ulong style;
        public ulong color;//color_class
        public long borderThickness;
        public float opacity;
        public ulong serverEntityCount;
        // 13
        public float score;
        // 14
        public ulong teamColor;//color_class
        public float mothershipX;
        public float mothershipY;
        public ulong mothership;
        // 15
        public ulong playercount;
        public string[] playerids;
        public string[] playernames;
        public override string ToString()
        {
            var output = "";
            if (fieldgroups[FIELDS.Score])
            {
                output += "player_tank";
            }
            else if (fieldgroups[FIELDS.Health] && fieldgroups[FIELDS.Physics])
            {
                if (sides == 1)
                {
                    output += "bullet";
                }
                if (sides == 2)
                {
                    output += "2 sides";
                }
                if (sides == 3)
                {
                    output += "triangle";
                }
                if (sides == 4)
                {
                    output += "rectangle";
                }
                if (sides == 5)
                {
                    output += "pentagon";
                }
            }
            else if (fieldgroups[FIELDS.Name])
            {
                output += "tank";
            }
            if (fieldgroups[FIELDS.Barrel])
            {
                output += $" shooting:{shooting} reloadTime:{reloadTime} damageFactor:{shootingAngle}";
            }
            if (fieldgroups[FIELDS.Physics])
            {
                output += $" Object:{Object} sides:{sides} size:{size} width:{width} knockbackFactor:{knockbackFactor} damageFactor:{damageFactor}";
            }
            if (fieldgroups[FIELDS.Health])
            {
                output += $" healthbar:{healthbar} health:{health} maxhealth:{maxHealth}";
            }
            if (fieldgroups[6])
            {
                output += $" unknown:{unknown}";
            }
            if (fieldgroups[FIELDS.PlayerInfo])
            {
                output += $"playercount:{playercount} playernames:{string.Join(",", playernames)} playerdescriptions:{string.Join(",", playerids)}";
            }
            if (fieldgroups[FIELDS.Arena])
            {
                output += $"GUI:{GUI} scoreboardAmount:{scoreboardAmount} scoreboardnames:{string.Join(",", scoreboardNames)} scoreboardscores:{string.Join(",", scoreboardScores)}";
            }
            if (fieldgroups[FIELDS.Name])
            {
                output += $" name:{name} nametag:{nametag}";
            }
            if (fieldgroups[FIELDS.GUI])
            {
                output += $"FOV:{FOV} level:{level} tank:{tank} statsAvailable:{level} cameraX:{cameraX} cameraY:{cameraY}";
            }
            if (fieldgroups[FIELDS.Position])
            {
                output += $" x:{x} y:{y} angle:{angle} motion:{motion}";
            }
            if (fieldgroups[FIELDS.Display])
            {
                if (style == 9 && color == 14 && borderThickness == 640 && opacity == 1)
                {
                    output = output.Insert(0, "wall");
                }
                output += $" style:{style} color:{color} borderThickness:{borderThickness} opacity:{opacity} serverEntityCount:{serverEntityCount}";
            }
            if (fieldgroups[FIELDS.Score])
            {
                output += $" score:{score}";
            }
            if (fieldgroups[FIELDS.Mothership])
            {
                output += $" teamColor:{teamColor} mothershipX:{mothershipX} mothershipY:{mothershipY} mothership:{mothership}";
            }
            return output;
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            lock (textLayoutlock)
            {
                disposed = true;
                if (nametextLayout != null && !nametextLayout.IsDisposed)
                    nametextLayout.Dispose();
                if (scoretextLayout != null && !scoretextLayout.IsDisposed)
                    scoretextLayout.Dispose();
            }
        }
    }
    public class INFO
    {
        public static string build;
        public static readonly string currentfolder = System.AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string buildfile = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "build.txt.txt");
        public static readonly string headless_config_file = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "headless_config.txt.txt");
        public ulong MAGIC_NUM;
        public UInt64 UPTIME_XOR;
        public UInt64 DEL_COUNT_XOR;
        public UInt64 UPD_COUNT_XOR;
        public UInt64 RECV_PACKET_INDEX;
        public new_field[] FIELD_ORDER;
        public class new_field
        {
            public int fieldGroup;
            public int index;
            public string type;
            public string name;

            public new_field(int fieldGroup, int index, string type, string name)
            {
                this.fieldGroup = fieldGroup;
                this.index = index;
                this.type = type;
                this.name = name;
                if (string.IsNullOrWhiteSpace(name))
                    this.name = fieldGroup + "_" + index;
            }
            public override string ToString()
            {
                return name + " " + type;
            }
        }
        public string build_name;
        public static Color[] COLORS_RGB = new Color[] {
            Color.FromArgb(85, 85, 85),
            Color.FromArgb(153, 153, 153),
            Color.FromArgb(0, 178, 225  ),
            Color.FromArgb(0, 178, 225  ),
            Color.FromArgb(241, 78, 84  ),
            Color.FromArgb(191, 127, 245),
            Color.FromArgb(0, 225, 110  ),
            Color.FromArgb(138, 255, 105),
            Color.FromArgb(255, 232, 105),
            Color.FromArgb(252, 118, 119),
            Color.FromArgb(118, 141, 252),
            Color.FromArgb(241, 119, 221),
            Color.FromArgb(255, 232, 105),
            Color.FromArgb(67, 255, 145 ),
            Color.FromArgb(187, 187, 187),
            Color.FromArgb(241, 78, 84  ),
            Color.FromArgb(252, 195, 118),
            Color.FromArgb(192, 192, 192)
        };
        public static string[] COLORS = new string[] {
        "BASE_GRAY",//0
        "BARREL_GRAY",//1
        "BODY_BLUE",//2
        "TEAM_BLUE",//3
        "TEAM_RED",//4
        "TEAM_PURPLE",//5
        "TEAM_GREEN",//6
        "SHINY_GREEN",//7
        "SQUARE_YELLOW",//8
        "TRIANGLE_RED",//9
        "PENTAGON_PURPLE",//10
        "CRASHER_PINK",//11
        "CLOSER_YELLOW",//12
        "SCOREBOARD_GREEN",//13
        "MAZEWALL_GRAY",//14
        "FFA_RED",//15
        "NECRO_ORANGE",//16
        "FALLEN_GRAY"//17
        };
        public static string[] TANKS = new string[] {
                "Tank", // 0
		"Twin",
        "Triplet",
        "Triple Shot",
        "Quad Tank",
        "Octo Tank",
        "Sniper",
        "Machine Gun",
        "Flank Guard",
        "Tri-Angle",
        "Destroyer", // 10
		"Overseer",
        "Overlord",
        "Twin-Flank",
        "Penta Shot",
        "Assassin",
        "Arena Closer",
        "Necromancer",
        "Triple Twin",
        "Hunter",
        "Gunner", // 20
		"Stalker",
        "Ranger",
        "Booster", // 23
		"Fighter",//24
        "Hybrid",//25
        "Manager",//26
        "Mothership",//27
        "Predator",//28
        "Sprayer",
        "Predator X", // deleted
		"Trapper",
        "Gunner Trapper",
        "Overtrapper",
        "Mega Trapper",
        "Tri-Trapper",
        "Smasher",
        "", // deleted
		"Landmine",
        "Auto Gunner",
        "Auto 5",
        "Auto 3",
        "Spread Shot",
        "Streamliner",
        "Auto Trapper",
        "Dominator1", // Destroyer
		"Dominator2", // Gunner
		"Dominator3", // Trapper
		"Battleship",
        "Annihilator",
        "Auto Smasher",
        "Spike",
        "Factory",
        "Ball", // deleted / only on home screen
		"Skimmer",
        "Rocketeer"
        };
        public static INFO Setup_Field()
        {
            lock (buildfile)
            {
                if (File.Exists(buildfile))
                {
                    string text;
                    text = File.ReadAllText(buildfile);
                    var splitted = text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    INFO info = new INFO();
                    if (splitted[0] == "version: 1.4" && splitted.Length == 10)
                    {
                        info.build_name = splitted[2];
                        build = info.build_name;
                        if (!string.IsNullOrWhiteSpace(Program.settings.build) && build != Program.settings.build)
                        {
                            var result = MessageBox.Show("Remove eval folder contents?", "outdated build", MessageBoxButtons.YesNo);
                            if (result == DialogResult.Yes)
                            {
                                DirectoryInfo di = new DirectoryInfo("eval");

                                foreach (FileInfo file in di.GetFiles())
                                {
                                    file.Delete();
                                }
                            }
                        }
                        Program.settings.build = build;
                        info.MAGIC_NUM = ulong.Parse(splitted[3]);
                        info.UPTIME_XOR = ulong.Parse(splitted[4]);
                        info.DEL_COUNT_XOR = ulong.Parse(splitted[5]);
                        info.UPD_COUNT_XOR = ulong.Parse(splitted[6]);
                        var temp = splitted[7].Split(new[] { ", " }, StringSplitOptions.None);
                        info.swapkey = new Dictionary<int, int>();
                        for (int i = 0; i < temp.Length; i++)
                        {
                            info.swapkey.Add(i, int.Parse(temp[i]));
                        }
                        List<int> fields = splitted[8].Replace("(", "").Replace(")", "").Split(',').Select(int.Parse).ToList();
                        //(Owner, Barrel, Physics, Health, Unknown, Scores, Arena, Name, GUI, Position, Display, Score, Mothership, PlayerInfo)
                        int indextemp = 0;
                        FIELDS.Owner = fields[indextemp++];
                        FIELDS.Barrel = fields[indextemp++];
                        FIELDS.Physics = fields[indextemp++];
                        FIELDS.Health = fields[indextemp++];
                        FIELDS.Unknown = fields[indextemp++];
                        FIELDS.Scores = fields[indextemp++];
                        FIELDS.Arena = fields[indextemp++];
                        FIELDS.Name = fields[indextemp++];
                        FIELDS.GUI = fields[indextemp++];
                        FIELDS.Position = fields[indextemp++];
                        FIELDS.Display = fields[indextemp++];
                        FIELDS.Score = fields[indextemp++];
                        FIELDS.Mothership = fields[indextemp++];
                        FIELDS.PlayerInfo = fields[indextemp++];
                        info.FIELD_ORDER = JArray.Parse(splitted[9]).Children().Select(x => new new_field((int)x["Item1"], (int)x["Item2"], (string)x["Item3"], (string)x["Item4"])).ToArray();

                        //var list = new Regex("\"([a-zA-Z]+)\":\"*(?:([\\da-zA-Z[\\]]+)|\")").Matches(splitted[7]).Cast<Match>().Select(x => x.Groups[2].Value).ToArray();
                        //info.FIELD_ORDER = new new_field[list.Length / 4];
                        //for (int i = 0; i < list.Length; i += 4)
                        //{
                        //    info.FIELD_ORDER[i / 4] = new new_field() { fieldGroup = int.Parse(list[i]), index = int.Parse(list[i + 1]), type = list[i + 2], name = list[i + 3] };
                        //    if (string.IsNullOrWhiteSpace(info.FIELD_ORDER[i / 4].name))
                        //        info.FIELD_ORDER[i / 4].name = info.FIELD_ORDER[i / 4].fieldGroup + "," + info.FIELD_ORDER[i / 4].index;
                        //}
                        //info.SetupOrderINFO();
                        info.FIELD_ORDER = info.FIELD_ORDER
  .GroupBy(f => f.fieldGroup * 100 + f.index)
  .Select(g => g.First())
  .ToArray();
                        for (int i = 0; i < 16; i++)
                        {
                            List<int> groups = new List<int>() { i };
                            var test = info.FIELD_ORDER.Where(x => groups.Contains(x.fieldGroup)).Select(x => x.ToString()).ToArray();
                            Debug.WriteLine(string.Join(", ", test));
                        }
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Arena, FIELDS.Mothership });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.PlayerInfo });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.GUI });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Physics, FIELDS.Health, FIELDS.Name, FIELDS.Position, FIELDS.Display });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Physics, FIELDS.Health, FIELDS.Name, FIELDS.Position, FIELDS.Display, FIELDS.Score });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Barrel, FIELDS.Physics, FIELDS.Position, FIELDS.Display });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.GUI });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Physics, FIELDS.Health, FIELDS.Position, FIELDS.Display });
                        //info.GenerateSortedGroup(new List<int>() { 8, 4 });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Physics, FIELDS.Position, FIELDS.Display });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Mothership });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Owner, FIELDS.Barrel, FIELDS.Position });
                        //info.GenerateSortedGroup(new List<int>() { FIELDS.Physics, FIELDS.Position, FIELDS.Display });
                    }
                    else
                    {
                        File.Delete(buildfile);
                        DirectoryInfo di = new DirectoryInfo("eval");

                        foreach (FileInfo file in di.GetFiles())
                        {
                            file.Delete();
                        }
                        return Setup_Field();
                    }
                    return info;
                }
                else
                {
                    return null;
                }
            }
        }
        public Dictionary<int, int> swapkey = new Dictionary<int, int>();
        public void GenerateSortedGroup(List<int> groups)
        {
            //var grouppedFields = groups.Select((i) => INFO._groups[i]).ToList();
            //var fields = new List<string>();
            sortedgroups.Add(groups, FIELD_ORDER.Where(x => groups.Contains(x.fieldGroup)).ToArray());
            //List<table_class> data = new List<table_class>();
            //var fieldgroups = new bool[16];
            /*for (int i = 0; i < grouppedFields.Count; i++)
            {
                fieldgroups[groups[i]] = true;
                fields.AddRange(grouppedFields[i]);
            }
            fields.Sort((f1, f2) => Array.IndexOf(FIELD_ORDER, f1) - Array.IndexOf(FIELD_ORDER, f2));
            sortedgroups.Add(groups, fields);*/
        }
        public Dictionary<List<int>, new_field[]> sortedgroups = new Dictionary<List<int>, new_field[]>(new MyEqualityComparer());
    }
    public class MyEqualityComparer : IEqualityComparer<List<int>>
    {
        public bool Equals(List<int> x, List<int> y)
        {
            if (x.Count != y.Count)
            {
                return false;
            }
            for (int i = 0; i < x.Count; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(List<int> obj)
        {
            int result = 17;
            for (int i = 0; i < obj.Count; i++)
            {
                unchecked
                {
                    result = result * 23 + obj[i];
                }
            }
            return result;
        }
    }
    #endregion js
}
