using Newtonsoft.Json.Linq;
using SharpDX.Direct2D1;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebsocket;
using static DiepClient.POWSolver;
using static DiepClient.MathHelper;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.IO;

namespace DiepClient
{
    class HcaptchaSolver
    {
        public static WatsonWsServer Hcaptchaserver;
        private static Object outputLock = new Object();
        public static TaskCompletionSource<string> captcha = new TaskCompletionSource<string>();
        public static string Solvecaptcha()
        {
            lock (outputLock)
            {
                if (Hcaptchaserver != null && Hcaptchaserver.ListClients().Count() > 0)
                {
                    captcha = new TaskCompletionSource<string>();
                    Hcaptchaserver.SendAsync(Hcaptchaserver.ListClients().First().Guid, "test").Wait();
                    Task.WhenAny(captcha.Task, Task.Delay(60000)).Wait();
                    if (captcha.Task.IsCompleted)
                    {
                        var result = captcha.Task.Result;
                        return result;
                    }
                }
            }
            return null;
        }
        public static void init()
        {
            try
            {
                Hcaptchaserver = new WatsonWsServer("localhost", 8650);//8599
                Hcaptchaserver.ClientConnected += (object sender, ConnectionEventArgs e) =>
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Black, "Hcaptcha Connected"));
                };
                Hcaptchaserver.MessageReceived += (object sender, MessageReceivedEventArgs e) =>
                {
                    try
                    {
                        var result = Encoding.UTF8.GetString(e.Data.Array);
                        captcha.SetResult(result);
                        lock (HeadlessMain.captchasolves)
                            HeadlessMain.captchasolves.Enqueue(result);
                    }
                    catch (Exception idk)
                    {
                        Debug.WriteLine(idk.ToString());
                    }
                };
                Hcaptchaserver.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        //public static string
    }
    class EvalSolver
    {
        public static Dictionary<string, byte[]> evallist = new Dictionary<string, byte[]>();
        static SHA256 sHA256 = SHA256.Create();
        public static string GetSha256Hash(string input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = sHA256.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
        public static WatsonWsServer evalserver;
        public static TaskCompletionSource<byte[]> evalbytes;
        private static Object outputLock = new Object();
        public static byte[] eval(string data)
        {
            //var idk = StringCipher.Encrypt(StringCipher.CompressString(data), "unauthenticatedunautnenticatedunauthenticated");
            //return HeadlessMain.SendGetEval(idk).Result;
            lock (evallist)
                if (evallist.ContainsKey(GetSha256Hash(data)))
                    return evallist[GetSha256Hash(data)];
            //if (File.Exists(Path.Combine("eval/", GetSha256Hash(data) + "")))
            //    return File.ReadAllBytes(Path.Combine("eval/", GetSha256Hash(data) + ""));
            lock (outputLock)
            {
                if (evalserver != null && evalserver.ListClients().Count() > 0)
                {
                    evalbytes = new TaskCompletionSource<byte[]>();
                    evalserver.SendAsync(evalserver.ListClients().First().Guid, data).Wait();
                    Task.WhenAny(evalbytes.Task, Task.Delay(10000)).Wait();
                    if (evalbytes.Task.IsCompleted)
                    {
                        var result = evalbytes.Task.Result;
                        File.WriteAllBytes(Path.Combine("eval/", GetSha256Hash(data) + ""), result);
                        return result;
                    }
                }
            }
            return null;
        }
        public static void init()
        {
            DirectoryInfo di = new DirectoryInfo("eval");
            if (!di.Exists)
                di.Create();
            foreach (FileInfo file in di.GetFiles())
            {
                evallist.Add(file.Name, File.ReadAllBytes(file.FullName));
            }
            if (evallist.Count < 200)
            {
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        evalserver = new WatsonWsServer("localhost", 8602 + i * 2);
                        evalserver.ClientConnected += (object sender, ConnectionEventArgs e) =>
                        {
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Black, "Eval Connected"));
                        };
                        evalserver.MessageReceived += (object sender, MessageReceivedEventArgs e) =>
                        {
                            try
                            {
                                evalbytes.SetResult(e.Data.Array.Take(e.Data.Count).ToArray());
                            }
                            catch (Exception idk)
                            {
                                Debug.WriteLine(idk.ToString());
                            }
                        };
                        evalserver.Start();
                        break;
                    }
                    catch (Exception ex)
                    {
                        var w32ex = ex as Win32Exception;
                        if (w32ex != null)
                        {
                            int code = w32ex.ErrorCode;
                            if (code != 183)
                            {
                                MessageBox.Show(ex.ToString());
                                break;
                            }
                        }
                        else
                        {
                            MessageBox.Show(ex.ToString());
                            break;
                        }
                    }
                }
            }
        }
    }
    class POWSolver
    {
        [DllImport("cpowsolve.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern void initmain();
        [DllImport("cpowsolve.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int solve(byte[] output, string prefix, int difficulty);
        public static WatsonWsServer powserver;
        public static TaskCompletionSource<byte[]> powbytes;
        public static Object outputLock = new Object();
        public static bool Active = true;
        public static Queue<powobject> powdataQueue;
        public class powobject
        {
            public byte[] solve;
            public DateTime returntime;
            public Action<byte[]> callback;
            public int id;

            public powobject(byte[] solve, DateTime returntime, Action<byte[]> callback, int id)
            {
                this.solve = solve;
                this.returntime = returntime;
                this.callback = callback;
                this.id = id;
            }
        }
        //public static readonly Queue<Tuple<string, Action<byte[]>>> powdataQueue = new Queue<Tuple<string, Action<byte[]>>>();
        public static byte[] solve(string data)
        {
            var count = powserver.ListClients().Count();
            var first = powserver.ListClients().FirstOrDefault();
            lock (outputLock)
            {
                if (powserver != null && count > 0)
                {
                    powbytes = new TaskCompletionSource<byte[]>();
                    powserver.SendAsync(first.Guid, data).Wait();
                    Task.WhenAny(powbytes.Task, Task.Delay(10000)).Wait();
                    if (powbytes.Task.IsCompleted)
                    {
                        return powbytes.Task.Result;
                    }
                }
            }
            return null;
        }
        public static void PowThread()
        {
            new Thread(() =>
            {
                powobject info = null;
                bool idkidk = false;
                while (Active)
                {
                    try
                    {
                        lock (powdataQueue)
                        {
                            if (powdataQueue.Count > 0 && (DateTime.Now).CompareTo(powdataQueue.Peek().returntime) >= 0)
                            {
                                info = powdataQueue.Dequeue();
                                idkidk = true;
                            }
                        }
                        if (idkidk)
                        {
                            //Stopwatch stopwatch = Stopwatch.StartNew();
                            //var output = solve(info.Item1);
                            //stopwatch.Stop();
                            //int sleeptime = (int)((9000 - stopwatch.ElapsedMilliseconds));
                            //if (accepted && sleeptime > 0 && wait)
                            //    instantpow.WaitOne(sleeptime);
                            //instantpow.Reset();
                            //Debug.WriteLine(Encoding.UTF8.GetString(output));
                            //if (output == null)
                            //{
                            //    Thread.Sleep(500);
                            //    output = solve(info.Item1);
                            //}
                            if (info.callback != null)
                                info.callback(info.solve);
                            idkidk = false;
                        }
                        else
                            Thread.Sleep(5);
                    }
                    catch (Exception ex)
                    {
                        StringCipher.LogError("solvethread.txt", ex.ToString());
                    }
                }
            })
            { IsBackground = true }.Start();
        }
        public static void init()
        {
            powdataQueue = new Queue<powobject>();
            if (Program.haspowdll)
                try
                {
                    initmain();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            else
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        powserver = new WatsonWsServer("localhost", 8603 + i * 2);
                        powserver.ClientConnected += (object sender, ConnectionEventArgs e) =>
                        {
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Black, "POW Connected"));
                        };
                        powserver.MessageReceived += (object sender, MessageReceivedEventArgs e) =>
                        {
                            try
                            {
                                powbytes.SetResult(e.Data.Array);
                            }
                            catch (Exception idk)
                            {
                                Debug.WriteLine(idk.ToString());
                            }
                        };
                        powserver.Start();
                        break;
                    }
                    catch (Exception ex)
                    {
                        var w32ex = ex as Win32Exception;
                        if (w32ex != null)
                        {
                            int code = w32ex.ErrorCode;
                            if (code != 183)
                            {
                                MessageBox.Show(ex.ToString());
                                break;
                            }
                        }
                        else
                        {
                            MessageBox.Show(ex.ToString());
                            break;
                        }
                    }
                }
            PowThread();
        }
    }
    [Serializable]
    public class leaderboard : IDisposable
    {
        //public Tuple<ulong[], string[], float[], long[], TextLayout[]> lb;
        public lbspot lb = new lbspot();
        public class lbspot
        {
            public ulong[] color;
            public string[] name;
            public float[] score;
            public long[] tank;
            public int lbamount = 0;
            public lbspot(ulong[] color, string[] name, float[] score, long[] tank)
            {
                this.color = color;
                this.name = name;
                this.score = score;
                this.tank = tank;
            }
            //public lbspot(ulong[] color, string[] name, long[] score, long[] tank, TextLayout[] textcache)
            //{
            //    this.color = color;
            //    this.name = name;
            //    this.score = score;
            //    this.tank = tank;
            //    this.textcache = textcache;
            //}
            public lbspot()
            {
                color = new ulong[10];
                name = new string[10];
                score = new float[10];
                tank = new long[10];
            }
        }
        public bool connecting = true;
        public string text;
        //public TextLayout textlayout;
        public System.Drawing.RectangleF rectangle;
        public string neutralparty = "";
        public List<string> parties = new List<string>();
        public bool updating;
        public string server;
        public int playercount;
        public int maxplayercount;
        public string region;
        public string gamemode;
        public DateTime starttimer;
        public System.Timers.Timer timer;
        public static void SortLBs()
        {
            lock (diep_directx.leaderboards)
                diep_directx.leaderboards.Sort((b, a) =>
                {
                    if (a.lb == null && b.lb == null)
                        return a.playercount.CompareTo(b.playercount) != 0 ? a.playercount.CompareTo(b.playercount) : a.text.CompareTo(b.text);
                    if (a.lb != null && b.lb == null)
                        return 1;
                    if (a.lb == null && b.lb != null)
                        return -1;
                    if (a.lb.lbamount == 0 && b.lb.lbamount == 0)
                        return 0;
                    if (a.lb.lbamount != 0 && b.lb.lbamount == 0)
                        return 1;
                    if (a.lb.lbamount == 0 && b.lb.lbamount != 0)
                        return -1;
                    return a.lb.score[0].CompareTo(b.lb.score[0]);
                });
        }
        //public void SetLB(lbspot lb)
        //{
        //    if (this.lb != null)
        //        Disposelb();
        //    if (lb != null)
        //        this.lb = new lbspot(lb.color, lb.name, lb.score, lb.tank, lb.name.Select((x, s) => diep_directx.GetStringDraw(diep_directx.GetLeaderBoardValue(x, lb.score[s]), diep_directx.scoreboardlistformat)).ToArray());
        //}
        public void SetLB(lbspot lb)
        {
            //if (this.lb != null)
            //    Disposelb();
            if (lb != null)
                this.lb = new lbspot(lb.color, lb.name, lb.score, lb.tank);
        }
        /*public leaderboard(Tuple<ulong[], string[], float[], long[]> lb, string text, System.Drawing.RectangleF rectangle, List<string> parties, bool updating, string id)
        {
            if (lb != null)
                this.lb = new Tuple<ulong[], string[], float[], long[], TextLayout[]>(lb.color, lb.name, lb.score, lb.tank, lb.name.Select((x, s) => GetStringDraw(GetLeaderBoardValue(x, lb.score[s]), scoreboardformat)).ToArray());
            this.text = text;
            this.textlayout = GetStringDraw(text, scoreboardformat);
            this.rectangle = rectangle;
            this.parties = parties;
            this.updating = updating;
            this.server = id;
        }*/
        public leaderboard(lbspot lb, string text, System.Drawing.RectangleF rectangle, string party, bool updating, string id)
        {
            if (lb != null)
                this.lb = new lbspot(lb.color, lb.name, lb.score, lb.tank);//new lbspot(lb.color, lb.name, lb.score, lb.tank, lb.name.Select((x, s) => diep_directx.GetStringDraw(diep_directx.GetLeaderBoardValue(x, lb.score[s]), diep_directx.scoreboardlistformat)).ToArray());
            this.text = text;
            //this.textlayout = diep_directx.GetStringDraw(text, diep_directx.scoreboardlistformat);
            this.rectangle = rectangle;
            this.neutralparty = party;
            this.updating = updating;
            this.server = id;
        }
        public leaderboard()
        {
        }
        public void AddParty(string party)
        {
            if (!parties.Contains(party))
                parties.Add(party);
        }
        public string ToLb()
        {
            var lbstring = "";
            for (int i = 0; i < lb.lbamount; i++)
            {
                if (i == 0)
                    lbstring += Environment.NewLine;
                lbstring += lb.color[i] + "start" + lb.name[i].ToString() + "end" + lb.score + " " + lb.tank[i];
                if (i != lb.lbamount - 1)
                    lbstring += Environment.NewLine;
            }
            return gamemode + " " + region + " " + playercount + " " + (timer != null ? (int)starttimer.AddMilliseconds(timer.Interval).Subtract(DateTime.Now).TotalMinutes : 0) + "m" + lbstring;
        }
        public void Disposelb()
        {
            /*if (lb != null)
                lock (lb)
                {
                    if (lb != null && lb.textcache != null)
                        for (int i = 0; i < lb.textcache.Length; i++)
                        {
                            if (lb.textcache[i] != null && !lb.textcache[i].IsDisposed)
                                lb.textcache[i].Dispose();
                        }
                    lb = null;
                }*/
        }
        public void Dispose()
        {
            //if (textlayout != null && !textlayout.IsDisposed)
            //    textlayout.Dispose();
            Disposelb();
        }
    }
    public class HeadlessMain
    {
        public static Dictionary<long, tankinfo> bulletspeeds = new Dictionary<long, tankinfo>();
        public struct tankinfo
        {
            public int bullettype;
            public float bulletdelay;
            public float bulletreload;
            public float bulletrecoil;
            public float bullethealth;
            public float bulletdamage;
            public float bulletspeed;
            public float bulletscatter;
            public float bulletlifelength;
            public float bulletabsorbtionfactor;
            public List<int> upgrades;

            public tankinfo(string bullettype, double bulletdelay, double bulletreload, double bulletrecoil, double bullethealth, double bulletdamage, double bulletspeed, double bulletscatter, double bulletlifelength, double bulletabsorbtionfactor, List<int> upgrades)
            {
                this.bullettype = bullettype == "bullet" ? 0 : bullettype == "drone" ? 1 : 2;
                this.bulletdelay = (float)bulletdelay;
                this.bulletreload = (float)bulletreload;
                this.bulletrecoil = (float)bulletrecoil;
                this.bullethealth = (float)bullethealth;
                this.bulletdamage = (float)bulletdamage;
                this.bulletspeed = (float)bulletspeed;
                this.bulletscatter = (float)bulletscatter;
                this.bulletlifelength = (float)bulletlifelength;
                this.bulletabsorbtionfactor = (float)bulletabsorbtionfactor;
                this.upgrades = upgrades;
            }
        }
        public static string[] tokens = new string[];
        public static async Task<Tuple<bool, string>> fetch(string url, string body, string method, IWebProxy? proxy = null)
        {
			throw new Exception();
        }
        //public static List<Headless_Client> clients = new List<Headless_Client>();
        public static ConcurrentDictionary<int, Headless_Client> clients = new ConcurrentDictionary<int, Headless_Client>();
        public static bool norenderclients = false;
        public static bool localhost = true;
        public static int botcount = 0;
        public static int bot_id = 0;
        //public static string[] gamemodes = new string[] { "ffa", "survival", "teams", "4teams", "dom", "tag", "maze", "sandbox" };
        public static string[] gamemodes = new string[] { "ffa", "teams", "4teams", "maze", "event", "sandbox" };
        public static string[] randomgamemodes = new string[] { "teams", "4teams", "ffa", "maze" };
        //public static string[] gamemodes = new string[] { "ffa" };
        //public static string[] randomgamemodes = new string[] { "ffa" };
        //public static string[] gamemodes = new string[] { "sandbox" };
        //public static string[] randomgamemodes = new string[] { "sandbox" };
        //public static string[] gamemodes = new string[] { "4teams" };
        //public static string[] randomgamemodes = new string[] { "4teams" };
        //public static string[] randomregions = new string[] { "lnd-sfo", "lnd-fra", "lnd-atl", "lnd-syd", "lnd-tok" };
        //public static string[] regions = new string[] { "lnd-sfo", "lnd-fra", "lnd-atl", "lnd-syd", "lnd-tok" };
        public static string[] randomregions = new string[] { "fra" };
        public static string[] regions = new string[] { "fra" };
        public static int proxycount = 0;
        public static Dictionary<ulong, Tuple<int, int>> playerlocations = new Dictionary<ulong, Tuple<int, int>>();
        public static HashSet<ulong> mytanks = new HashSet<ulong>();
        public static string correctteam = "";
        public static bool isoncorrectteam = false;
        public static List<string> pushbottarget = null;
        public static float mintargetscore = 0;
        public static string botname = "";
        public static bool gogetkey = false;
        public enum BotMode
        {
            Manual,
            PushBot,
            Observer,
            Stall,
            AFK,
        }
        public static Tuple<DateTime, Tuple<bool, string>, int> cache = new Tuple<DateTime, Tuple<bool, string>, int>(DateTime.MinValue, null, 0);
        public static List<string> connected = new List<string>();
        private static int currentrandomgamemode = 0;
        private static int currentregion = 0;
        public static Tuple<Tuple<Uri, string, bool, byte[], string>, string, string> FetchNextRandomGame()
        {
            var gamemode = randomgamemodes[currentrandomgamemode];
            var region = randomregions[currentregion];
            currentrandomgamemode++;
            if (currentrandomgamemode >= randomgamemodes.Length)
            {
                currentrandomgamemode = 0;
                currentregion++;
                if (currentregion >= randomregions.Length)
                {
                    currentregion = 0;
                }
            }
            return new Tuple<Tuple<Uri, string, bool, byte[], string>, string, string>(FetchGetGame(null, gamemode, region, null, null).Result, gamemode, region);
        }
        public class Lobby
        {
            public bool successful;
            public string message;
            public string party;

            public Lobby(bool successful, string message, string party)
            {
                this.successful = successful;
                this.message = message;
                this.party = party;
            }
        }
        public static List<string> parties = new List<string>();
        public static Queue<string> captchasolves = new Queue<string>();
        public static async Task DDOSGame(string party)
        {
            var currentlobby = GetLobby(party);
            string originalobby = currentlobby.Item1;
            byte[] originalparty = currentlobby.Item2;
            var data = await FetchGetGame(currentlobby.Item1, currentlobby.Item3, currentlobby.Item4, null, originalparty);
            if (data.Item1 == null)
            {
                Debugger.Break();
                return;
            }
            var count = 0;
            for (int i = 0; i < 200; i++)
            {
                Task.Run(() => HeadlessMain.ConnectToLobbyV2(data));
                count++;
                if (count % 10 == 0)
                    Debug.WriteLine(count);
            }
            while (true)
            {
                Task.Run(() => HeadlessMain.ConnectToLobbyV2(data));
                Thread.Sleep(20);
                count++;
                if (count % 10 == 0)
                    Debug.WriteLine(count);
            }
        }
        public static async Task UpdateListV2(bool update, bool cacheonly = false)
        {
            var time = 1800;
            if (cache.Item1.AddSeconds(time).CompareTo(DateTime.Now) >= 0)
            {
                return;
            }
            async Task GetRandomGame()
            {
                //for (int i = 0; i < 8; i++)
                //{
                var data = HeadlessMain.FetchNextRandomGame();

                if (data.Item1 != null && data.Item1.Item3)
                {
                    var serverid = data.Item1.Item5;
                    var partylink = Headless_Client.GetPartyCode(serverid);
                    bool contains = false;
                    lock (clients)
                        if (clients.Any(x => partylink == x.Value.GetNeutralPartyCode()))
                            contains = true;
                    if (!contains)
                    {
                        leaderboard? lb = null;
                        lock (diep_directx.leaderboards)
                        {
                            lb = diep_directx.leaderboards.FirstOrDefault(x => x.server == serverid);
                            if (lb == null)
                            {
                                lb = new leaderboard(null, data.Item2 + " " + data.Item3, default, partylink, false, serverid)
                                {
                                    gamemode = data.Item2,
                                    region = data.Item3,
                                };
                                diep_directx.leaderboards.Add(lb);
                            }
                            else
                            {
                                //??
                            }
                        }
                        HeadlessMain.ConnectToLobbyV2(data.Item1, true, lb);
                    }
                }
                else
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Red, "Captcha Blocked"));
                    //break;
                }
                //}
                System.Timers.Timer runonce = new System.Timers.Timer(1000 * 5);// 60);
                runonce.Elapsed += (s, eventtimer) => { GetRandomGame().Wait(); };
                runonce.AutoReset = false;
                runonce.Start();
            }
            await GetRandomGame();
        }
        public static Lobby ConnectToLobbyV2(Tuple<Uri, string, bool, byte[], string> fetchedgame, bool lbbot = false, leaderboard leaderboard = null)
        {
            var temp = "";
            //lock (proxylist)
            temp = Program.proxylist[random.Next(Program.proxylist.Count)];
            IWebProxy proxy = null;
            if (!string.IsNullOrWhiteSpace(temp))
            {
                proxy = new WebProxy(temp, true);
            }
            var cipher = new Diep_Encryption.Cipher();
            if (fetchedgame != null)
            {
                if (fetchedgame.Item1 == null)
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, fetchedgame.Item2));
                    if (fetchedgame.Item2 == "RateLimited")
                    {
                        return new Lobby(false, "RateLimited", null);
                    }
                    if (!string.IsNullOrWhiteSpace(temp))
                    {
                        //lock (proxylist)
                        //    proxylist.Remove(temp);
                        return null;
                        //continue;
                    }
                    //if (state.ShouldExitCurrentIteration)
                    //{
                    //        // some other thread called state.Break()
                    //        return;
                    //}
                    //state.Break();
                    return null;
                    //continue;
                }
                diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(2), diep_directx.Gray, "Connecting to lobby"));
                var headless = new Headless_Client(info, fetchedgame.Item1, origin, fetchedgame.Item2, bot_id++, fetchedgame.Item4, cipher, proxy);
                if (leaderboard != null)
                {
                    clients.TryAdd(headless.id, headless);
                    headless.parser.leaderboard = leaderboard;
                    if (headless.connectedtoserver.WaitOne(30000))
                        if (headless.connectedtoserver_success)
                        {
                            if (!string.IsNullOrWhiteSpace(correctteam) && headless.playertankcolor.WaitOne(30000) && headless.team == correctteam)
                                return new Lobby(true, correctteam, headless.GetPartyCode());
                            else
                                return new Lobby(true, null, headless.GetPartyCode());
                        }
                        else if (headless.errortype == Headless_Client.Errortype.notfound)
                        {
                            return new Lobby(false, headless.errortype.ToString(), null);
                        }
                    return null;
                }
                else if (lbbot)
                {
                    headless.leaderboardbot = true;
                    if (headless.connectedtoserver.WaitOne(30000))
                    {
                        if (headless.connectedtoserver_success)
                        {
                            var amount = (int)headless.parser.scoreboardAmount;
                            return new Lobby(true, "", headless.GetPartyCode());
                        }
                        else if (headless.errortype == Headless_Client.Errortype.notfound)
                        {
                            return new Lobby(false, headless.errortype.ToString(), headless.GetPartyCode());
                        }
                    }
                    return null;
                }
            }
            return null;
        }
        public static async Task<Lobby> ConnectToLobby(Tuple<string, byte[], string, string> currentlobby, byte[] originalparty, bool bypassplayercount = false, Uri originalurl = null, List<string> proxylist = null, bool lbbot = false, Tuple<Uri, string, bool, byte[], string> current = null, leaderboard leaderboard = null)
        {
            var temp = "";
            //lock (proxylist)
            temp = proxylist[random.Next(proxylist.Count)];
            IWebProxy proxy = null;
            if (!string.IsNullOrWhiteSpace(temp))
            {
                temp = temp.Replace("http://", "").Replace("socks4://", "");
                Debug.WriteLine(temp);
                proxy = new WebProxy(temp, true);
            }
            current = await FetchGetGame(currentlobby.Item1, currentlobby.Item3, currentlobby.Item4, proxy, originalparty);
            bypassplayercount = false;
            var cipher = new Diep_Encryption.Cipher();
            Lobby Connect()
            {
                if (current != null)
                {
                    if (current.Item1 == null)
                    {
                        diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, current.Item2));
                        if (current.Item2.ToLower().Contains("ratelimited"))
                        {
                            return new Lobby(false, "RateLimited", null);
                        }
                        if (!string.IsNullOrWhiteSpace(temp))
                        {
                            //lock (proxylist)
                            //    proxylist.Remove(temp);
                            return null;
                            //continue;
                        }
                        //if (state.ShouldExitCurrentIteration)
                        //{
                        //        // some other thread called state.Break()
                        //        return;
                        //}
                        //state.Break();
                        return null;
                        //continue;
                    }
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(2), diep_directx.Gray, "Connecting to lobby"));
                    var headless = new Headless_Client(info, (!bypassplayercount ? current.Item1 : originalurl), origin, current.Item2, bot_id++, current.Item4, cipher, proxy);
                    if (leaderboard != null)
                    {
                        clients.TryAdd(headless.id, headless);
                        headless.parser.leaderboard = leaderboard;
                        if (headless.connectedtoserver.WaitOne(30000))
                            if (headless.connectedtoserver_success)
                            {
                                if (!string.IsNullOrWhiteSpace(correctteam) && headless.playertankcolor.WaitOne(30000) && headless.team == correctteam)
                                    return new Lobby(true, correctteam, headless.GetPartyCode());
                                else
                                    return new Lobby(true, null, headless.GetPartyCode());
                            }
                            else if (headless.errortype == Headless_Client.Errortype.notfound)
                            {
                                return new Lobby(false, headless.errortype.ToString(), null);
                            }
                        return null;
                    }
                    else if (lbbot)
                    {
                        headless.leaderboardbot = true;
                        if (headless.connectedtoserver.WaitOne(30000))
                        {
                            if (headless.connectedtoserver_success)
                            {
                                var amount = (int)headless.parser.scoreboardAmount;
                                return new Lobby(true, "", headless.GetPartyCode());
                            }
                            else if (headless.errortype == Headless_Client.Errortype.notfound)
                            {
                                return new Lobby(false, headless.errortype.ToString(), headless.GetPartyCode());
                            }
                        }
                        return null;
                    }
                    else
                    {
                        //lock (clients)
                        //{
                        clients.TryAdd(headless.id, headless);
                        if (headless.connectedtoserver.WaitOne(30000))
                            if (headless.connectedtoserver_success)
                            {
                                if (!string.IsNullOrWhiteSpace(correctteam) && headless.playertankcolor.WaitOne(30000) && headless.team == correctteam)
                                    return new Lobby(true, correctteam, headless.GetPartyCode());
                                else
                                    return new Lobby(true, null, headless.GetPartyCode());
                            }
                            else if (headless.errortype == Headless_Client.Errortype.notfound)
                            {
                                return new Lobby(false, headless.errortype.ToString(), null);
                            }
                        return null;
                        //}
                    }
                }
                return null;
            }
            //for (int i = 0; i < 1000; i++)
            //if (current != null)
            //{
            //    if (current.Item1 != null)
            //    {
            //        Parallel.For(0, 50000, (i) =>
            //        {
            //            //Debug.WriteLine("Start" + i);
            //            //new Thread(() =>
            //            //{
            //            //new Task(() =>
            //            //{
            //            //Debug.WriteLine("Start" + i);
            //            new Headless_Client(info, (!bypassplayercount ? current.Item1 : originalurl), origin, "", bot_id++, current.Item4, cipher, proxyurl);
            //            //}).Start();
            //            //{ IsBackground = true }.Start();
            //        });
            //    }
            //}
            return Connect();
        }
        public static string GetNewUrl(string originalobby, string region)
        {
            return "wss://" + originalobby + "-default.lobby.lnd-" + region + ".hiss.io/";
        }
        /*public static async Task<Lobby> GetLeaderBoard(Tuple<string, byte[], string, string> lobby, string region)
        {
            bool leaderboardparsed = false;
            var currentlobby = lobby;
            string originalobby = currentlobby.Item1;
            byte[] originalparty = currentlobby.Item2;
            Uri originalurl = null;
            if (true)
            {
                var lobby2 = GetRandomLobby(currentlobby.Item1, region);
                if (lobby2 == null || lobby2.Item2 == null || lobby2.Item1 == null)
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, "unable to bypass"));
                    return null;
                }
                originalurl = new Uri(GetNewUrl(originalobby, lobby2.Item2));
                currentlobby = new Tuple<string, byte[], string, string>(lobby2.Item1, currentlobby.Item2, currentlobby.Item3, currentlobby.Item4);
            }
            var bypassplayercount = true;
            var count = 0;
            var current = new Tuple<Uri, string, bool, byte[], string>(originalurl, "", true, currentlobby.Item2, currentlobby.Item1);
            while (!leaderboardparsed)
            {
                if (diep_directx.leaderboards.Count == 0)
                    return null;
                var idk = await ConnectToLobby(currentlobby, originalparty, bypassplayercount, originalurl, proxylist, true, current);
                count++;
                if (idk != null)
                    if (idk.message == "RateLimited")
                        Thread.Sleep(20000);
                    else
                        return idk;
                //if (true)
                //{
                //    if (count > 50)
                //        return null;
                //}
                //else
                //{
                if (count > 5)
                    return null;
                Thread.Sleep(5000);
                //}
            }
            return null;
        }*/
        public static async Task<Lobby> GetLeaderBoardV2(Tuple<string, byte[], string, string> lobby, string region, leaderboard leaderboard)
        {
            bool leaderboardparsed = false;
            var currentlobby = lobby;
            string originalobby = currentlobby.Item1;
            byte[] originalparty = currentlobby.Item2;
            Uri originalurl = null;
            var bypassplayercount = false;
            /*if (bypassplayercount)
            {
                var lobby2 = await GetRandomLobby(currentlobby.Item1, region);
                if (lobby2 == null || lobby2.Item2 == null || lobby2.Item1 == null)
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, "unable to bypass"));
                    return null;
                }
                originalurl = new Uri(GetNewUrl(originalobby, lobby2.Item2));
                currentlobby = new Tuple<string, byte[], string, string>(lobby2.Item1, currentlobby.Item2, currentlobby.Item3, currentlobby.Item4);
            }*/
            var count = 0;
            while (!leaderboardparsed)
            {
                if (diep_directx.leaderboards.Count == 0)
                    return null;
                diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, "Connecting lb"));
                var idk = await ConnectToLobby(currentlobby, originalparty, bypassplayercount, originalurl, Program.proxylist, true, null, leaderboard);
                count++;
                if (idk != null)
                    if (idk.message == "RateLimited")
                        Thread.Sleep(20000);
                    else
                        return idk;
                //if (true)
                //{
                //    if (count > 50)
                //        return null;
                //}
                //else
                //{
                if (count > 5)
                    return null;
                Thread.Sleep(5000);
                //}
            }
            return null;
        }
        public static Tuple<string, byte[], string, string> GetLobby(string party, string region = null, string game_mode = null)
        {
            string server = "";
            byte[] partyid;
            if (!string.IsNullOrWhiteSpace(party) && party.Contains("?"))
            {
                party = party.Replace("https://", "").Replace("diep.io", "").Replace("?", "").Replace("/", "").Replace("p=", "");
                if (party.Length > 32)
                    partyid = Writer.cstr(party.Substring(32));
                else
                    partyid = new byte[] { 0 };
                int k = 0;
                if (party.Length >= 32)
                    server = party.Substring(0, 32).Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-");
                //if (party.Length >= 72)
                //    server = Encoding.UTF8.GetString(party.Take(72).ToLookup(c => k++ / 2).Select(e => byte.Parse(new String(e.ToArray()), System.Globalization.NumberStyles.HexNumber)).Select(x => (byte)(((x & 0x0F) << 4 | (x & 0xF0) >> 4))).ToArray());
            }
            else
            {
                var random = new Random();
                if (string.IsNullOrWhiteSpace(game_mode))
                {
                    game_mode = randomgamemodes[random.Next(randomgamemodes.Length - 1)];
                }
                if (string.IsNullOrWhiteSpace(region))
                {
                    region = randomregions[random.Next(randomregions.Length)];
                }
                partyid = new byte[] { 0 };
            }
            return new Tuple<string, byte[], string, string>(server, partyid, game_mode, region);
        }

        public static async Task<Tuple<Uri, string, bool, byte[], string>> FetchGetGame(string server = null, string gamemode = "", string region = "", IWebProxy proxy = null, byte[] partyid = null)
        {
            string playertoken = "";
            Uri uri = null;
            Tuple<bool, string> idk = null;
            string captcha;
            lock (captchasolves)
            {
                captcha = captchasolves.Count > 0 ? "{\"turnstile\":{\"client_response\":\"" + captchasolves.Dequeue() + "\"}}" : "null";
            }
            if (!string.IsNullOrWhiteSpace(server))
            {
                idk = fetch("https://api.rivet.gg/matchmaker/lobbies/join", "{\"lobby_id\":\"" + server + "\",\"captcha\":" + captcha + "}", "POST", proxy).Result;
            }
            else
            {
                idk = fetch("https://api.rivet.gg/matchmaker/lobbies/find", "{\"game_modes\":[\"" + gamemode + "\"],\"regions\":[\"" + region + "\"],\"captcha\":" + captcha + "}", "POST", proxy).Result;
            }
            if (idk == null)
            {
                return null;
            }
            if (!idk.Item1)
            {
                if (idk.Item2.Contains("CAPTCHA_CAPTCHA_REQUIRED"))
                {
                    if (HcaptchaSolver.Hcaptchaserver != null)
                    {
                        diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Green, "Getting Captcha."));
                        var idk2 = HcaptchaSolver.Solvecaptcha();
                        if (idk2 != null)
                        {
                            return (await FetchGetGame(server, gamemode, region, proxy, partyid));
                        }
                    }
                    return new Tuple<Uri, string, bool, byte[], string>(null, idk.Item2, false, null, server);
                }
                else
                {
                    return new Tuple<Uri, string, bool, byte[], string>(null, idk.Item2, false, null, server);
                }
            }
            var responseidk = JObject.Parse(idk.Item2);
            dynamic response = responseidk;
            playertoken = response.lobby.player.token.Value;
            uri = new Uri("wss://" + response.lobby.ports["default"].host.Value);
            return new Tuple<Uri, string, bool, byte[], string>(uri, playertoken, true, partyid, response.lobby.lobby_id.Value);
        }
        static object filelock = new object();
        public static bool connecting = false;
        public static List<int> correctconnectedamount = new List<int>();
        public static INFO info = INFO.Setup_Field();
        static string origin = "https://diep.io";
        public static ConcurrentDictionary<uint, Object_class> Objects = new ConcurrentDictionary<uint, Object_class>();
        static Random random = new Random();
        static Thread connectingthread = null;
        public static void init(string party = "", int botcount = 1, string team = "", string name = "", bool bypassplayercount = false, bool useproxy = false)
        {
            if (connecting)
            {
                connectingthread.Interrupt();
                connectingthread.Join();
                connecting = false;
            }
            if (!connecting)
            {
                connectingthread = new Thread(async () =>
                {
                    try
                    {
                        connecting = true;
                        lock (HeadlessMain.correctconnectedamount)
                            if (string.IsNullOrWhiteSpace(team))
                                correctconnectedamount = clients.Values.Where(x => x.connected && x.accepted && x.connectedtoserver_success).Select(x => x.id).ToList();
                            else
                                correctconnectedamount = clients.Values.Where(x => x.connected && x.accepted && x.team == team).Select(x => x.id).ToList();
                        if (EvalSolver.evallist.Count < 200 && EvalSolver.evalserver.ListClients().Count() == 0 && false)//REMOVE
                        {
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Red, "Eval Server not connected and less than 200 evals"));
                            connecting = false;
                            return;
                        }
                        if (!Program.haspowdll && POWSolver.powserver.ListClients().Count() == 0)
                        {
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Red, "Pow Server not connected"));
                            connecting = false;
                            return;
                        }
                        botname = name;
                        correctteam = team;
                        if (string.IsNullOrWhiteSpace(party))
                        {
                        }
                        else if (!string.IsNullOrWhiteSpace(party) && party.Contains("?") && party.Length >= 32)
                        {
                        }
                        else
                        {
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Red, "Party link invalid"));
                            connecting = false;
                            return;
                        }
                        var currentlobby = GetLobby(party);
                        string originalobby = currentlobby.Item1;
                        byte[] originalparty = currentlobby.Item2;
                        Uri originalurl = null;
                        async Task<int> ConnectBots(int botcount2)
                        {
                            var exitvalue = 0;
                            //Parallel.For(0, botcount, (i, state) =>
                            //var list = new List<bool>();
                            for (int i3 = 0; i3 < botcount2; i3++)
                            {
                                //var stopwatch1 = Stopwatch.StartNew();
                                //List<Thread> threads = new List<Thread>();

                                //for (int i = 0; i < 4; i++)
                                //{
                                //var thread = new Thread(() =>
                                //{
                                var count = 0;
                            start:
                                if (exitvalue == 1)
                                    return exitvalue;
                                if (!string.IsNullOrWhiteSpace(team))
                                {
                                    var isoncorrectteam = clients.Values.Any(x => x.team == team);
                                    if (isoncorrectteam)
                                    {
                                        var first = clients.First(x => x.Value.team == team).Value;
                                        var party2 = first.GetPartyCode();
                                        currentlobby = GetLobby(party2);
                                        originalobby = currentlobby.Item1;
                                        originalparty = currentlobby.Item2;
                                        originalurl = null;
                                    }
                                }
                                var idk = await ConnectToLobby(currentlobby, originalparty, bypassplayercount, originalurl, Program.proxylist);
                                if (idk == null)
                                {
                                    count++;
                                    if (count > 5)
                                    {
                                        exitvalue = 1;
                                        return exitvalue;
                                    }
                                    else
                                    {
                                        Thread.Sleep(5000);
                                        goto start;
                                    }
                                }
                                if (!idk.successful)
                                {
                                    if (idk.message == "RateLimited")
                                    {
                                        Thread.Sleep(20000);
                                    }
                                    goto start;
                                    //exitvalue = 1;
                                    //return exitvalue;
                                    //IMPLEMENT
                                }
                                //})
                                //{
                                //    IsBackground = true
                                //};
                                //threads.Add(thread);
                                //thread.Start();
                                //}
                                //}//);
                                //foreach (Thread thread in threads)
                                //{
                                //    thread.Join();
                                //}
                                //thread.Join();
                                //stopwatch1.Stop();
                                //int wait = (int)(10000 - stopwatch1.ElapsedMilliseconds);
                                //if (wait > 0)
                                //    Thread.Sleep(wait);
                            }
                            return exitvalue;
                        }
                        var previouscount = clients.Count;
                        var previouscorrectcount = correctconnectedamount;
                        while (correctconnectedamount.Count < botcount)
                        {
                            var exitvalue = await ConnectBots(botcount - correctconnectedamount.Count);
                            if (exitvalue == 1)
                            {
                                diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Red, "Couldn't connect bots"));
                                break;
                            }
                            previouscount = clients.Count;
                            previouscorrectcount = correctconnectedamount;
                        }
                        var removeids = new List<int>();
                        foreach (var item in clients)
                        {
                            if (item.Value.accepted)
                                if (string.IsNullOrWhiteSpace(correctteam))
                                    continue;
                                else if (item.Value.playertankcolor.WaitOne(30000) && item.Value.team == correctteam)
                                    continue;
                            removeids.Add(item.Key);
                        }
                        foreach (var item in removeids)
                        {
                            clients[item].Close();
                        }
                        diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Gray, "Finished connecting bots"));
                        connecting = false;
                        //if (TorProcess.Container != null && !TorProcess.HasExited)
                        //    TorProcess.Kill();
                        //var diep_directx = new Headless_Client(info, uri, origin, null);
                        //clients.Add(diep_directx);
                        //while (true)
                        //{
                        //    Task[] tasks = new Task[350];
                        //    for (int i = 0; i < tasks.Length; i++)
                        //    {
                        //        tasks[i] = new Task(() =>
                        //        {
                        //            var diep_directx = new Headless_Client(info, current.Item1, origin, current.Item2, bot_id++, proxylist[proxycount++ % proxylist.Count]);
                        //            lock (clients)
                        //            {
                        //                clients.Add(diep_directx);
                        //            }
                        //            int c = 0;
                        //            while (true)
                        //            {
                        //                if (diep_directx.accepted || diep_directx.closed)
                        //                {
                        //                    break;
                        //                }
                        //                c++;
                        //                Thread.Sleep(10);
                        //                if (c > 50)
                        //                    break;
                        //            }
                        //        });
                        //        tasks[i].Start();
                        //        //clients.Add(new Headless_Client(info, uri, origin, proxylist[i]));
                        //        //clients.Add(new Headless_Client(info, uri, origin, proxylist[i]));
                        //    }
                        //    Task.WaitAll(tasks);
                        //    //for (int i = 0; i < clients.Count; i++)
                        //    //{
                        //    //    clients[i].Close();
                        //    //}
                        //    clients.Clear();
                        //}
                    }
                    catch (Exception ex)
                    {
                        if (ex.GetType() != typeof(System.Threading.ThreadAbortException))
                            StringCipher.LogError("connecterror.txt", ex.ToString());
                    }
                })
                {
                    IsBackground = true
                };
                connectingthread.Start();
            }
            else
                diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Gray, "Already connecting bots"));
        }
    }
    public class Headless_Client
    {
        ~Headless_Client()
        {
            Close();
        }
        //WebSocket_Client client;
        WebsocketClient client;
        //Powsolve powsolve = new Powsolve();
        public int id;
        public int num;
        INFO info;
        public Uri uri;
        string origin_url;
        IWebProxy proxy;
        public Parser parser;
        Diep_Encryption.Cipher cipher;
        public bool connected = false;
        public bool accepted = false;
        public string gamemode = "";
        private bool sandbox = false;
        public ulong playercount;
        private ulong spawning = 0;
        public bool hastank = false;
        public ulong? tank_id = 0;
        public tankgoal tankgoal = null;
        private string previous_stat = "";
        private string previous_tank = "";
        public string team = "";
        //readonly tankgoal octo = new tankgoal(new string[] { "Flank Guard", "Quad Tank", "Octo Tank" }, "565656565656564444444777777788888");
        //static readonly tankgoal SspSpread = new tankgoal("SspSpread", new string[] { "Twin", "Triple Shot", "Spread Shot" }, "565656565656567777777888888833232");
        //static readonly tankgoal Spread = new tankgoal("Spread", new string[] { "Twin", "Triple Shot", "Spread Shot" }, "565656565656564444477777778888888");
        //static readonly tankgoal Rocketeer = new tankgoal("Rocketeer", new string[] { "Machine Gun", "Destroyer", "Rocketeer" }, "565656565656564444477777778888888", 45, 45);
        //static readonly tankgoal Machinegun = new tankgoal("Machinegun", new string[] { "Machine Gun", "", "" }, "565656565656564444444888887777777", 15, 60, 60);
        //static readonly tankgoal Twin = new tankgoal("Twin", new string[] { "Twin", "", "" }, "565656565656564444444888888877777", 15, 60, 60);
        //static readonly tankgoal Destroyer = new tankgoal("Destroyer", new string[] { "Machine Gun", "Destroyer", "" }, "565656565656564444444888888877777");
        //static readonly tankgoal Fighter = new tankgoal("Fighter", new string[] { "Flank Guard", "Tri-Angle", "Fighter" }, "565656565656564444477777778888888");
        public static tankgoal Pushtank = new tankgoal("Pushtank", new string[] { "Flank Guard", "Tri-Angle", "Booster" }, "88888887777777", 30);
        public static List<tankgoal> tankgoals;/* = new List<tankgoal>
        {
            SspSpread,
            Rocketeer,
            Destroyer,
            Fighter,
            Machinegun,
            Twin
        };*/
        public HeadlessMain.BotMode botmode = HeadlessMain.BotMode.AFK;
        public int custom_right_click = 0;
        public bool protmode = false;
        public bool farmmode = false;
        public bool pushbot_multitarget = false;
        public List<pushtank> pushbot_ids = new List<pushtank>();
        public class pushtank
        {
            public ulong id;
            public ulong color;
            public int x;
            public int y;

            public pushtank(ulong id, ulong color, int x, int y)
            {
                this.id = id;
                this.color = color;
                this.x = x;
                this.y = y;
            }
        }
        public bool pushbotmousefollow = false;
        public pushbotmode pushbotmodenum = 0;
        public enum pushbotmode
        {
            Outbase,
            Inbase,
        }
        public bool receive = false;
        public bool isinvisible = false;
        public int timespentonlocation = 0;
        public bool invisiblesearch = false;
        public int searchx = 0;
        public int searchy = 0;
        public int currentsearchindex = 0;
        public bool invisible_haslocation = false;
        public int invisible_lastknownx = 0;
        public int invisible_lastknowny = 0;
        public bool pushleader = false;
        public double lastchangex = 0;
        public double lastchangey = 0;
        public double lastplayerx = 0;
        public double lastplayery = 0;
        public double predicted_x = 0;
        public double predicted_y = 0;
        public double previousspeed = 0;
        public int previousangle = 0;
        public int currentangle = 0;
        public double[] lastmovex = new double[36];
        public double[] lastmovey = new double[36];
        public bool shoot = true;
        public bool spin = false;
        public int spinangle = 0;
        public bool autoaim = false;
        public bool aimassist = false;
        public bool observerrespawn = false;
        public ulong observerdied_tick = 0;
        private ulong upgrade_tick = 0;
        private ulong stat_tick = 0;
        private ulong client_tick = 0;
        private int farm_tick = 0;
        private int pushbot_tick = 0;
        public bool right_click = false;
        public int bot_move_x = 0;
        public int bot_move_y = 0;
        public int actual_bot_move_x = 0;
        public int actual_bot_move_y = 0;
        public int bot_shoot_x = 0;
        public int bot_shoot_y = 0;
        public int actual_bot_shoot_x = 0;
        public int actual_bot_shoot_y = 0;
        public int custom_bot_move_x = 0;
        public int custom_bot_move_y = 0;
        public bool leaderboardbot = false;
        public bool connectedtoserver_success = false;
        public bool manualclose;
        public ManualResetEvent connectedtoserver = new ManualResetEvent(false);
        public ManualResetEvent playertankcolor = new ManualResetEvent(false);
        public bool closed = false;
        private Input input;
        private byte[] firstpow = null;
        private bool eval_done = false;
        private string evalid = "";
        private bool firstpow_done = false;
        private long last_ping = 0;
        public bool haskeyname = false;
        private string sendkeystring = "givemeyourkey";
        private long last_update = 0;
        public double pingtime = 0;
        public double server_speed = 1;
        public int server_speed_index = 0;
        public double[] server_speeds = Enumerable.Repeat(1d, 50).ToArray();//new double[50];
        public byte[] party;
        public string region;
        public static readonly string[] tank_colors = new string[] { "FFA_RED", "TEAM_BLUE", "TEAM_RED", "TEAM_PURPLE", "TEAM_GREEN" };
        private bool instantpow = true;
        private ManualResetEvent updatereceived = new ManualResetEvent(false);
        public ManualResetEvent packetsent = new ManualResetEvent(false);
        public string playertoken;
        //public static Map map = new Map();
        public enum Errortype
        {
            nothing,
            notfound
        }
        public Errortype errortype = Errortype.nothing;
        Random random = new Random();
        //character count = 15
        //public const string messagestring = "Collection of protocol, memory, and other hacky information for the browser game diep.io. What started off as an attempt to parse game leaderboards out of packets is now a collection of information about the insides of diep.io. Includes information such as packet encoding / decoding, packet protocol, memory structures, a bit of physics, wasm parsing, game code reversal, and much more.";
        //public const string messagestring = "Check my messages lol my guy is mute";
        //public static string[] messagestringsplitted = messagestring.Split(' ');
        //public static string[] messagestringsplitted = new string[] { "Im casually", "reminding these", "******* that im", "best ironically" };
        //public static string[] messagestringsplitted = new string[] { "Paranoia's all", "I got left" };
        public static string[] messagestringsplitted = new string[] { $"{HeadlessMain.botname}" };
        //public static string[] messagestringsplitted = new string[] { "" };
        public Headless_Client(INFO info, Uri uri, string url, string playertoken, int id, byte[] party, Diep_Encryption.Cipher cipher, IWebProxy proxy = null)
        {
            //this.cipher = cipher;//.copy();
            messagestringsplitted = new string[] { $"{HeadlessMain.botname}" };
            this.info = info;
            this.uri = uri;
            region = new Regex(@"lobby\.lnd-([a-z]+)\.hiss\.io").Match(uri.AbsoluteUri)?.Groups[1]?.Value;//FIX
            origin_url = url;
            this.playertoken = playertoken;
            this.id = id;
            this.proxy = proxy;
            if (party == null)
                party = new byte[1] { 0 };
            this.party = party;
            Start();
        }
        public void Start()
        {
            closed = false;
            spawning = 0;
            accepted = false;
            connected = false;
            cipher = new Diep_Encryption.Cipher();
            if (receive)
                parser = new Parser(info, true);
            input = new Input();
            Client_ServerConnected();
        }
        public bool fastclose = false;
        private void Client_ServerDisconnected(DisconnectionInfo info)
        {
            if (!receive)
                return;
            Debug.WriteLine("Disconnected " + info.Type.ToString() + " " + info?.Exception);
            if (fastclose)
                return;
            accepted = false;
            connected = false;
            closed = true;
            hastank = false;
            if (parser != null)
                parser.Dispose();
            lock (cipher.s_lock)
            {
                if (!closed)
                    client.Dispose();
                closed = true;
            }
            //lock (HeadlessMain.clients)
            //{
            var i = HeadlessMain.clients.FirstOrDefault(x => x.Key == id);
            if (i.Key == id)
                HeadlessMain.clients.TryRemove(i.Key, out _);
            //}
            connectedtoserver.Set();
        }
        public void Close()
        {
            manualclose = true;
            accepted = false;
            connected = false;
            hastank = false;
            if (parser != null)
                parser.Dispose();
            lock (cipher.s_lock)
            {
                if (!closed)
                    client.Dispose();
                closed = true;
            }
            var i = HeadlessMain.clients.FirstOrDefault(x => x.Key == id);
            if (i.Key == id)
                HeadlessMain.clients.TryRemove(i.Key, out _);
            connectedtoserver.Set();
        }
        public bool sent_tick = false;
        public void Tick()
        {
            client_tick++;
            if (!client.running || closed)
            {
                closed = true;
                hastank = false;
                tank_id = null;
                return;
            }
            if (!connected || !accepted)
            {
                botmode = HeadlessMain.BotMode.AFK;
                tank_id = null; hastank = false; return;
            }
            if (parser.leaderboard != null)
            {
                //if (client_tick > 2000 && parser.playercount <= 1)
                //{
                //    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(5), diep_directx.Gray, "Closing due to playeramount: " + parser.playercount));
                //    Close();
                //    return;
                //}
                if (client_tick % 10 != 0)
                    return;
            }
            Object_class GUI;
            //lock (parser)
            //{
            if (parser.GUI_id == null || !parser.all.ContainsKey(parser.GUI_id.Value))
            {
                tank_id = null; hastank = false; return;
            }
            GUI = parser.all[parser.GUI_id.Value];
            //}
            Object_class player = null;
            //lock (parser)
            if (GUI.player != null && parser.player_id != null && parser.all.ContainsKey(parser.player_id.Value) && (!hastank || tank_id == null))
            {
                tank_id = parser.player_id;
                hastank = true;
                player = parser.all[parser.player_id.Value];
                team = INFO.COLORS[player.color];
                //if (map.server != uri.AbsoluteUri)
                //{
                //    map.server = uri.AbsoluteUri;
                //    map.arenaBottomY = parser.arenaBottomY / Map.squaresize;
                //    map.arenaLeftX = parser.arenaLeftX / Map.squaresize;
                //    map.arenaRightX = parser.arenaRightX / Map.squaresize;
                //    map.arenaTopY = parser.arenaTopY / Map.squaresize;
                //    //if (gamemode == "maze")
                //    //{
                //    //    map.maze = true;
                //    //    map.CreateMap(parser.all);
                //    //}
                //}
                if (player.color >= 3 && player.color <= 6)
                {
                    lock (diep_directx.control_groups[(int)player.color - 1])
                        if (!diep_directx.control_groups[(int)player.color - 1].Any(x => x.id == id))
                            diep_directx.control_groups[(int)player.color - 1].Add(this);
                }
                else if (player.color == 2)
                    lock (diep_directx.control_groups[(int)player.color])
                        if (!diep_directx.control_groups[(int)player.color].Any(x => x.id == id))
                            diep_directx.control_groups[(int)player.color].Add(this);
                if (team == HeadlessMain.correctteam)
                {
                    lock (HeadlessMain.correctconnectedamount)
                        if (!HeadlessMain.correctconnectedamount.Contains(id))
                            HeadlessMain.correctconnectedamount.Add(id);
                    HeadlessMain.isoncorrectteam = true;
                }
                playertankcolor.Set();
                if (!HeadlessMain.connecting && !string.IsNullOrWhiteSpace(HeadlessMain.correctteam) && team != HeadlessMain.correctteam && HeadlessMain.isoncorrectteam)
                {
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Darkgray, "Spawned on wrong team: " + INFO.COLORS[player.color] + " Disconnecting"));
                    Close();
                    return;
                }
            }
            else if (GUI.player == null || parser.player_id == null || !parser.all.ContainsKey(parser.player_id.Value))
            {
                tank_id = null; hastank = false;
            }
            else
            {
                player = parser.all[parser.player_id.Value];
                parser.cameraX = player.x;
                parser.cameraY = player.y;
            }
            if (player == null && ((botmode == HeadlessMain.BotMode.Observer && !observerrespawn) || parser.leaderboard != null))
            {
                if (observerdied_tick == 0 && (client_tick - spawning) > 30)
                    observerdied_tick = client_tick;
                if ((client_tick - observerdied_tick) < 25 * 280)
                    return;
                else if ((client_tick - spawning) > 30)
                    observerrespawn = true;
            }
            if ((client_tick - spawning) > 100)
            {
                //lock (parser)
                if (player == null)
                {
                    if ((botmode != HeadlessMain.BotMode.Observer && parser.leaderboard == null) || observerrespawn)
                    {
                        previous_stat = "";
                        previous_tank = "";
                        if (botmode == HeadlessMain.BotMode.Observer || parser.leaderboard != null)
                        {
                            botmode = HeadlessMain.BotMode.AFK;
                            observerrespawn = false;
                            observerdied_tick = 0;
                        }
                        if (!string.IsNullOrWhiteSpace(team))
                        {
                            Object_class teambase = null;//red == 10146 11150 //blue -10146 -11150
                                                         //lock (parser)
                            teambase = parser.all.Values.FirstOrDefault(x => x.Object == 74 && x.color == (ulong)Array.IndexOf(INFO.COLORS, team));
                            //if ((gamemode == "4teams" && Distance((long)GUI.cameraX, (long)GUI.cameraY, teambase.x, teambase.y) > 10000) || (gamemode == "teams" && Distance((long)GUI.cameraX, 0, teambase.x, 0) > 10000))
                            //{
                            //    botmode = HeadlessMain.BotMode.AFK;
                            //    lock (diep_directx.selected)
                            //    {
                            //        var index = diep_directx.selected.FindIndex(x => x.id == id);
                            //        if (index != -1)
                            //            diep_directx.selected.RemoveAt(index);
                            //    }
                            //}
                            if (invisiblesearch)
                            {
                                if (currentsearchindex > 1)
                                {
                                    if (teambase.y > 0)
                                        searchy -= (int)(70 * 2);
                                    else
                                        searchy += (int)(70 * 2);
                                    currentsearchindex--;
                                }
                            }
                        }
                        //Debug.WriteLine("Spawn");
                        num = HeadlessMain.botcount;
                        if (HeadlessMain.gogetkey || botmode == HeadlessMain.BotMode.Stall)
                        {
                            Send(new byte[] { 2 }.Concat(Writer.cstr("GetKey")).ToArray());
                            haskeyname = true;
                        }
                        else
                        {
                            Send(new byte[] { 2 }.Concat(Writer.cstr(messagestringsplitted[HeadlessMain.botcount++ % messagestringsplitted.Length])).ToArray());
                            haskeyname = false;
                        }
                        //Send(new byte[] { 2 }.Concat(Writer.purestring("12345678901234567890")).ToArray());
                        //Send(new byte[] { 2 }.Concat(Writer.cstr((HeadlessMain.botcount++) + "")).ToArray());
                        //Send(new byte[] { 2 }.Concat(Writer.cstr("not a bot ^" + diep_directx.botcount++)).ToArray());
                        //Send(new byte[] { 2, 80, 82, 78, 72, 77, 84, 82, 0 });
                        spawning = client_tick;
                        bool found = false;
                        lock (POWSolver.powdataQueue)
                            for (int i = 0; i < POWSolver.powdataQueue.Count; i++)
                            {
                                var idk = POWSolver.powdataQueue.ElementAt(i);
                                if (idk.id == id && idk.callback != null)
                                {
                                    idk.callback(idk.solve);
                                    idk.callback = null;
                                    found = true;
                                    break;
                                }
                            }
                        if (!found)
                            instantpow = true;
                    }
                }
            }
            if (player != null)
            {
                HandleInputs(GUI, player);
                //Debug.WriteLine("Input");
                var idk = input.GetInput(client_tick, player);
                if (idk != null)
                {
                    Send(idk);
                    sent_tick = true;
                }
            }
            //else
            //{
            //    HandleInputs(GUI, player);
            //    var idk = input.GetInput(client_tick, player);
            //    if (idk != null)
            //    {
            //        Send(idk);
            //        sent_tick = true;
            //    }
            //}
        }
        public static string GetPartyCode(string server, byte[] party = null)
        {
            var output = "diep.io?p=";
            output += server.Replace("-", "");
            if (party != null)
            {
                output += ToHexadecimalRepresentation(party.Select(x => (byte)(((x & 0x0F) << 4 | (x & 0xF0) >> 4))).ToArray()).ToLower();
            }
            return output;
        }
        public static string ToHexadecimalRepresentation(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder(bytes.Length << 1);
            foreach (byte b in bytes)
            {
                sb.AppendFormat("{0:X2}", b);
            }
            return sb.ToString();
        }
        public string GetNeutralPartyCode()
        {
            return GetPartyCode(new string(uri.Host.Take(36).ToArray()));
        }
        public string GetPartyCode()
        {
            return GetPartyCode(new string(uri.Host.Take(36).ToArray()), party);
        }
        public void UpgradeStat(int index, int max_upg)
        {
            List<byte> buffer = new List<byte>
            {
                3,
                (byte)GetActualStatXor(index),
                (byte)max_upg
            };
            Send(buffer.ToArray());
        }
        public void UpgradeTank(string tankname)
        {
            List<byte> buffer = new List<byte>
            {
                4,
                (byte)GetActualUpgradeXor(Array.IndexOf(INFO.TANKS, tankname))
            };
            Send(buffer.ToArray());
        }
        public int GetActualStatXor(int index)
        {
            var num = (long)(info.MAGIC_NUM & 7) ^ 8 - index;
            var num2 = (num << 1) ^ (num >> 31);
            var num3 = num2 >> 7;
            var num4 = (num2 & 127) | ((num3 != 0 ? 1 : 0) << 7);
            return (int)num4;
        }
        public int GetActualUpgradeXor(int index)
        {
            var num = (long)(info.MAGIC_NUM % 54) ^ index;
            var num2 = (num << 1) ^ (num >> 31);
            var num3 = num2 >> 7;
            var num4 = (num2 & 127) | ((num3 != 0 ? 1 : 0) << 7);
            return (int)num4;
        }
        public static int GetActualStatIndex(int index)
        {
            return 8 - index;
        }
        public void Ping()
        {
            if (parser == null || parser.leaderboard != null) return;
            if (!connected || !accepted) return;
            //if (client_tick % 10 == 0 && last_ping == 0)
            if (last_ping == 0)
            {
                Send(new byte[] { 05 });
                last_ping = DateTime.Now.Ticks;
            }
        }
        bool blocksend = false;
        int amountsent = 0;
        public void Send(byte[] bytes, bool force = false)
        {
            if (client.running && !closed)
            {
                //Debug.WriteLine(bytes.Length);
                //Debug.WriteLine("Send " + amountsent++ + " Raw " + string.Join(", ", bytes));
                lock (cipher.s_lock)
                {
                    //packetsent.Reset();
                    bytes = cipher.Encrypt(bytes);
                    //Debug.WriteLine("Send " + string.Join(", ", bytes));
                    //if (client.ready && !closed)
                    client.Send(bytes);//, ((completed) => { packetsent.Set(); }));
                }
            }
        }
        public void MoveTowards(Object_class player, long x, long y, long sensitivity = 400, bool gamepad = true, bool checkcollision = false)
        {
            //if (map.maze)
            //{
            //    var end = map.pathfinder.findPath(player, x, y, parser.all.Values.Where(z => z.fieldgroups[FIELDS.Position] && z.fieldgroups[FIELDS.Display] && z.fieldgroups[FIELDS.Health] && !z.fieldgroups[FIELDS.Barrel] && z.id != tank_id.Value).Select(o => new Pathfind_Object(player, o)).ToArray(), true);
            //    if (end != null && end.parent != null && end.parent.parent != null)
            //        while (end.parent != null)
            //        {
            //            //x = end.x * Map.wallsize - Map.wallhalf * Map.wallsize + Map.wallsize / 2;
            //            //y = end.y * Map.wallsize - Map.wallhalf * Map.wallsize + Map.wallsize / 2;
            //            x = end.x * Map.squaresize;// + Map.squaresize / 2;
            //            y = end.y * Map.squaresize;// + Map.squaresize / 2;
            //            sensitivity = 0;
            //            end = end.parent;
            //        }
            //}
            //gamepad = false;
            input.gamepad = gamepad;
            actual_bot_move_x = (int)x;
            actual_bot_move_y = (int)y;
            if (!gamepad)
            {
                var d_y = Distance(y, player.y);
                var d_x = Distance(x, player.x);
                if (Distance(d_y, d_x) > 200)
                {
                    if (d_y > d_x)
                    {
                        input.keyRight = false;
                        input.keyLeft = false;
                        if (player.y > (y + sensitivity))
                        {
                            input.keyDown = false;
                            input.keyUp = true;
                        }
                        else if (player.y < (y - sensitivity))
                        {
                            input.keyUp = false;
                            input.keyDown = true;
                        }
                        else
                        {
                            input.keyDown = false;
                            input.keyUp = false;
                        }
                    }
                    else
                    {
                        input.keyDown = false;
                        input.keyUp = false;
                        if (player.x > (x + sensitivity))
                        {
                            input.keyRight = false;
                            input.keyLeft = true;
                        }
                        else if (player.x < (x - sensitivity))
                        {
                            input.keyRight = true;
                            input.keyLeft = false;
                        }
                        else
                        {
                            input.keyRight = false;
                            input.keyLeft = false;
                        }
                    }
                }
                else
                {
                    if (player.y > (y + sensitivity))
                    {
                        input.keyDown = false;
                        input.keyUp = true;
                    }
                    else if (player.y < (y - sensitivity))
                    {
                        input.keyUp = false;
                        input.keyDown = true;
                    }
                    else
                    {
                        input.keyDown = false;
                        input.keyUp = false;
                    }
                    if (player.x > (x + sensitivity))
                    {
                        input.keyRight = false;
                        input.keyLeft = true;
                    }
                    else if (player.x < (x - sensitivity))
                    {
                        input.keyRight = true;
                        input.keyLeft = false;
                    }
                    else
                    {
                        input.keyRight = false;
                        input.keyLeft = false;
                    }
                }
            }
            else
            {
                input.keyRight = false;
                input.keyLeft = false;
                input.keyDown = false;
                input.keyUp = false;
                long vx = x - player.x;
                long vy = y - player.y;
                long cx = player.x - player.previous_x;
                long cy = player.y - player.previous_y;
                double dist = Distance(x, y, player.x + cy, player.y + cy);
                vx -= cx * 6;
                vy -= cy * 6;
                //const delta = {
                //        x: TARGET.x - player.x,
                //        y: TARGET.y - player.y
                //    }
                //Debug.WriteLine((x - player.x) + " " + (y - player.y));
                var max = (float)Math.Max(Math.Abs(vx), Math.Abs(vy));
                //Debug.WriteLine(vx + " " + vy);
                //double magnitude = Math.Abs(vx) + Math.Abs(vy);
                if (max == 0 || checkcollision && dist < sensitivity / 5)
                {
                    input.vx = 0;
                    input.vy = 0;
                    lastmovex[currentangle] = 0;
                    lastmovey[currentangle] = 0;
                }
                else
                {
                    var ivx = vx / max * (float)Math.Min(1d, dist / sensitivity);
                    var ivy = vy / max * (float)Math.Min(1d, dist / sensitivity);
                    input.vx = ivx;
                    input.vy = ivy;
                    lastmovex[currentangle] = ivx;
                    lastmovey[currentangle] = ivy;
                }
                //Debug.WriteLine(input.vx + " " + input.vy);
            }
        }
        int bytecount = 0;
        private void Client_MessageReceived(byte[] e)
        {
            //bytecount += e.Length;
            //Debug.WriteLine(bytecount + " " + (bytecount / 1000000000d));
            //if (packetcount < 10)
            //    File.WriteAllBytes("rawpacket" + packetcount, e);
            //Debug.WriteLine("Receive Raw " + string.Join(", ", e));
            try
            {
                lock (cipher.c_lock)
                {
                    e = cipher.Decrypt(e);
                    previouscipher = cipher;
                    if (e[0] == 0 || e[0] == 2)
                        MessageReceived(e);
                }
                if (e[0] != 0 && e[0] != 2)
                    MessageReceived(e);
                previousbuffer = e;
            }
            catch (Exception ex)
            {
                StringCipher.LogError("Client_MessageReceived.txt", ex.ToString());
            }
        }
        Diep_Encryption.Cipher previouscipher;
        byte[] previousbuffer;
        int m28count = 0;
        int packetcount = 0;
        private void MessageReceived(byte[] e)
        {
            Reader reader = new Reader();
            reader.buffer = e;
            var num = reader.parse_u8();
            if (packetcount < 10)
            {
                Debug.WriteLine(num + " " + e.Length);
                //File.WriteAllBytes("packet" + packetcount++, e);
                packetcount++;
            }
            //StringCipher.LogError("MessageReceived.txt", num + "");
            //Debug.WriteLine("Packet:" + string.Join(" ", e));
            switch (num)
            {
                case 0:
                    if (!receive)
                    {
                        Send(new byte[] { 2 }.Concat(Writer.cstr(messagestringsplitted[HeadlessMain.botcount++ % messagestringsplitted.Length])).ToArray());
                        Ping();
                        diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(1), diep_directx.Red, "Spawning"));
                        return;
                    }
                    //Debug.WriteLine(num);
                    if (parser.leaderboard == null)
                    {
                        long current = DateTime.Now.Ticks;
                        double temp_server_speed;
                        if ((current - last_update) != 0)
                        {
                            temp_server_speed = 40d / ((current - last_update) / (double)TimeSpan.TicksPerMillisecond);
                        }
                        else
                        {
                            temp_server_speed = -1;
                        }
                        last_update = current;
                        server_speeds[server_speed_index++ % server_speeds.Length] = temp_server_speed;
                        if (server_speed_index >= server_speeds.Length)
                            server_speed_index = 0;
                        server_speed = server_speeds.Average();
                    }
                    //Debug.WriteLine("Update Packet:" + string.Join(" ", e));
                    parser.at = 0;
                    var at = parser.parse(e);
                    if (at.HasValue)
                    {
                        //File.WriteAllBytes($"m28_{m28count++}.buf", e.Skip((int)at.Value).ToArray());
                        if ((reader.buffer[at.Value] & 2) != 0)
                        {
                            var value = (reader.buffer[at.Value] & 1) != 0 ? 40 : 2748;
                            //Debug.WriteLine("m28");
                            cipher.addoffset(value);
                        }
                    }

                    //File.WriteAllText("lb.txt", string.Join(", ", parser.scoreboardScores));
                    Tick();
                    Ping();
                    updatereceived.Set();
                    if (!connectedtoserver_success)
                    {
                        if (string.IsNullOrWhiteSpace(HeadlessMain.correctteam))
                            lock (HeadlessMain.correctconnectedamount)
                                if (!HeadlessMain.correctconnectedamount.Contains(this.id))
                                    HeadlessMain.correctconnectedamount.Add(this.id);
                        connectedtoserver_success = true;
                        connectedtoserver.Set();
                        if (leaderboardbot)
                        {
                            Close();
                            return;
                        }
                    }
                    break;
                case 1:
                    //reader.at++;
                    var idk = reader.getbytes(reader.buffer.Length - reader.at);
                    var stridk = Encoding.ASCII.GetString(idk, 0, idk.Length);
                    //idk = reader.parse_cstr();
                    //if (HeadlessMain.identificationkey == "keystartkirbysvmendkey")
                    //{
                    StringCipher.LogError("outdated client.txt", "Message " + e.Length);
                    System.Environment.Exit(1);
                    break;
                    //}
                    Debug.WriteLine("Outdated client:" + idk);
                    Debug.WriteLine("Outdated client:" + string.Join(" ", e));
                    MessageBox.Show("Outdated client");
                    break;
                case 2:
                    //Debug.WriteLine("Compressed packet:" + string.Join(" ", e));
                    Decompress decompress = new Decompress();
                    decompress.at = reader.at;
                    var decompressed = decompress.DecompressPacket(e);
                    //Debug.WriteLine("DeCompressed packet:" + string.Join(" ", decompressed));
                    MessageReceived(decompressed);
                    break;
                case 3:
                    var message = reader.parse_cstr();
                    var color = reader.parse_u32();
                    var duration = reader.parse_float();
                    var identifier = reader.parse_cstr();
                    lock (diep_directx.notifications)
                        if (!(message.Contains(" has been defeated by ") && diep_directx.notifications.Any(x => x.Item4.Contains(" has been defeated by "))) && !(message.EndsWith(" has spawned!") && diep_directx.notifications.Any(x => x.Item4.EndsWith(" has spawned!"))))
                            diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(duration / 1000), diep_directx.Black, message));
                    Debug.WriteLine("Message:" + message + " " + color + " " + duration + " " + identifier);
                    break;
                case 4:
                    gamemode = reader.parse_cstr();
                    sandbox = gamemode == "sandbox";
                    var host_region = reader.parse_cstr();
                    Debug.WriteLine("gameinfo: gamemode: " + gamemode + " host-region: " + host_region);
                    Debug.WriteLine(string.Join(",", e));
                    break;
                case 5:
                    long current2 = DateTime.Now.Ticks;
                    pingtime = ((current2 - last_ping) / (double)TimeSpan.TicksPerMillisecond);
                    sent_tick = false;
                    last_ping = 0;
                    //Ping
                    break;
                case 6:
                    //Debug.WriteLine("Party code:" + string.Join(" ", e));
                    party = e.Skip(1).ToArray();
                    var partycode = GetPartyCode();
                    lock (HeadlessMain.parties)
                    {
                        if (!HeadlessMain.parties.Contains(partycode))
                        {
                            HeadlessMain.parties.Add(partycode);
                            File.WriteAllText("currentparties.txt", string.Join(Environment.NewLine, HeadlessMain.parties));
                        }
                    }
                    break;
                case 7:
                    Debug.WriteLine("Accepted");
                    if (proxy != null)
                        File.AppendAllText("workingproxylist.txt", proxy.ToString() + Environment.NewLine);
                    accepted = true;
                    break;
                case 8:
                    var length = reader.parse_vu();
                    for (ulong i = 0; i < length; i++)
                    {
                        var achiement = reader.parse_cstr();
                        //Debug.WriteLine("Achiement: " + );
                    }
                    //Debug.WriteLine("Achiement:" + string.Join(" ", e));
                    break;
                case 9:
                    diep_directx.notifications.Add(new Tuple<DateTime, TimeSpan, SolidColorBrush, string>(DateTime.Now, TimeSpan.FromSeconds(10), diep_directx.Red, "Team Full"));
                    Debug.WriteLine("Error:" + string.Join(" ", e));//9 10 0
                    var error = reader.parse_vu();
                    var errormessage = reader.parse_cstr();
                    Debug.WriteLine(error + " " + errormessage);
                    break;
                case 10:
                    playercount = reader.parse_vu();
                    Debug.WriteLine("player count: " + playercount);
                    break;
                case 11:
                    byte diff = (byte)reader.parse_vu();
                    var str = reader.parse_cstr();
                    Debug.WriteLine("PoW Challenge " + diff + " " + str + " " + eval_done);
                    if (!eval_done)
                    {
                        firstpow = e;
                        break;
                    }
                    if (eval_done && !firstpow_done)
                    {
                        firstpow = e;
                        WebPOWSolve(str, diff, false);
                        firstpow_done = true;
                        break;
                    }
                    //Stopwatch stopwatch = new Stopwatch();
                    //stopwatch.Start();
                    //byte[] buffer = new byte[50];
                    //Task.Run(() =>
                    //{
                    //int outputlength = diep_directx.solve(buffer, str, diff);
                    //byte[] output = new byte[outputlength];
                    //Array.Copy(buffer, output, outputlength);
                    //var splitted = Encoding.UTF8.GetString(output).Split(':');
                    //Send(new byte[] { 10 }.Concat(Writer.cstr(splitted[1])).ToArray());
                    WebPOWSolve(str, diff, true);
                    //});
                    break;
                case 13:
                    var id = reader.parse_vu();
                    var text = reader.parse_cstr();
                    Debug.WriteLine("Int JS Challenge " + " " + id);// + " " + text);
                    //Debug.WriteLine("Int JS Challenge " + " " + id + " " + text);
                    //Task.Run(() =>
                    //{
                    evalid = EvalSolver.GetSha256Hash(text);
                    var result = EvalSolver.eval(text);
                    if (result == null)
                    {
                        client.exit();
                        break;
                    }
                    Debug.WriteLine("result " + result.Length + " " + string.Join(",", result));
                    List<byte> bytes = new List<byte>() { 11 };
                    bytes.AddRange(Writer.vu(id));
                    bytes.AddRange(Writer.cstr(Encoding.UTF8.GetString(result)));
                    Debug.WriteLine("result " + bytes.ToArray().Length);
                    Debug.WriteLine(string.Join(",", bytes.ToArray()));
                    Send(bytes.ToArray(), true);
                    eval_done = true;
                    if (!firstpow_done && firstpow != null)
                    {
                        Reader reader2 = new Reader();
                        reader2.at = 1;
                        reader2.buffer = firstpow;
                        byte diff2 = (byte)reader2.parse_vu();
                        var str2 = reader2.parse_cstr();
                        WebPOWSolve(str2, diff2, false);
                        firstpow_done = true;
                    }
                    //}).Wait();
                    break;
                default:
                    if (!receive)
                        return;
                    StringCipher.LogError("decryption error.txt", "Message " + e.Length);
                    parser.Dispose();
                    Debug.WriteLine(e.Length);
                    Debug.WriteLine("Message " + string.Join(" ", e));
                    Debug.WriteLine("Message " + Encoding.ASCII.GetString(e));
                    Debug.WriteLine("Message " + Encoding.UTF8.GetString(e));
                    File.WriteAllBytes("decryption error.buf", e);
                    File.WriteAllBytes("previous_decryption error.buf", previousbuffer);
                    Debug.WriteLine("Closing");
                    Close();
                    System.Environment.Exit(1);
                    break;
            }
            if (reader.canread() && (num != 0 && num != 2))
                Debug.WriteLine("empty bytes num: " + num + " amount: " + (e.Length - reader.at));
        }
        private void WebPOWSolve(string str, int diff, bool wait)
        {
            //new Thread(() =>
            Task.Run(() =>
            {
                try
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    byte[] buffer = new byte[50];
                    int outputlength;
                    byte[] solve;
                    if (Program.haspowdll)
                    {
                        lock (POWSolver.outputLock)
                            outputlength = POWSolver.solve(buffer, str, diff);

                        solve = new byte[outputlength];
                        Array.Copy(buffer, solve, outputlength);
                        //var solve = powsolve.getpow(str, diff);
                    }
                    else
                        solve = POWSolver.solve(str + "^" + diff);
                    stopwatch.Stop();
                    int sleeptime = 0;// 8000 - (int)stopwatch.ElapsedMilliseconds;
                    if (accepted && sleeptime > 0 && wait && !instantpow)
                    {
                        lock (POWSolver.powdataQueue)
                            //POWSolver.powdataQueue.Enqueue(new Tuple<string, Action<byte[]>>(str + "^" + diff, ((callback) =>
                            POWSolver.powdataQueue.Enqueue(new powobject(solve, DateTime.Now.AddMilliseconds(sleeptime), ((callback) =>
                            {
                                if (callback != null)
                                    Send(new byte[] { 10 }.Concat(callback).ToArray(), true);
                                else
                                    Close();
                            }), id));
                    }
                    else
                    {
                        Send(new byte[] { 10 }.Concat(solve).ToArray(), true);
                        instantpow = false;
                    }
                }
                catch (Exception ex)
                {
                    StringCipher.LogError("powsolve.txt", ex.ToString());
                }
            });
            //{
            //    IsBackground = true
            //}.Start();
        }
        private void Client_ServerConnected()
        {
            if (receive)
                Debug.WriteLine("Connected " + DateTime.Now.ToString("HH:mm"));
            if (proxy != null)
                File.AppendAllText("connectproxylist.txt", proxy.ToString() + Environment.NewLine);
            List<byte> bytes = new List<byte>();
            bytes.Add(0);
            //bytes.AddRange(Writer.cstr("d7c97d71cfd9fa045b6f5d4474fc99166a3f3a56"));
            bytes.AddRange(Writer.cstr(info.build_name));
            //if (diep_directx.localhost)
            //{
            //    bytes.AddRange(Writer.cstr("385353279191646209v798780ec60994a1e706944fc654ce6c1b008b264ccd7ee0a5b48e0f1e653ed33"));
            //}
            //else
            //bytes.AddRange(Writer.cstr(""));//password
            bytes.AddRange(party);
            //bytes.AddRange(Writer.cstr("16CE80D551"));//diep.io/#6643568303532366D263837353D243365613D226664666D21623630353532666832666530016CE80D551
            //bytes.AddRange(Writer.cstr(""));//party code
            //bytes.AddRange(Writer.cstr(File.ReadAllLines("playertokens.txt")[1]));
            bytes.AddRange(Writer.cstr(playertoken));
            Random random = new Random();
            //if (HeadlessMain.tokens.Length > 0)
            //    bytes.AddRange(Writer.cstr(Regex.Match(HeadlessMain.tokens[random.Next(HeadlessMain.tokens.Length)], @"(game_user\.[a-zA-Z0-9.\-_]+)").Groups[1].Value));
            //else
            //bytes.Add(0);
            bytes.AddRange(Writer.cstr("spike"));//??
                                                 
                                                 
            bytes.AddRange(Writer.cstr(""));//??
            bytes.AddRange(Writer.cstr(""));//??
            if (receive)
                Debug.WriteLine("Send Raw" + string.Join(", ", bytes));
            if (receive)
                Debug.WriteLine("Send Raw " + System.Convert.ToBase64String(bytes.ToArray()));
            client.Send(bytes.ToArray());//, null);
            connected = true;
        }
    }
    class Input
    {
        List<byte> previous = new List<byte>();
        public ulong counter = 0;
        public bool leftMouse;
        public bool keyUp;
        public bool keyLeft;
        public bool keyDown;
        public bool keyRight;
        public bool godMode;
        public bool suicide;
        public bool rightMouse;
        public bool levelUp;
        public bool gamepad;
        public bool switchClass;
        public float mouseX;
        public float mouseY;
        public float vx;
        public float vy;
        public byte[] GetInput(ulong client_tick, Object_class player = null)
        {
            int output = 0;
            if (keyDown && keyUp)
            {
                keyDown = false;
                keyUp = false;
            }
            if (keyLeft && keyRight)
            {
                keyLeft = false;
                keyRight = false;
            }
            output |= (true == true ? 1 : 0);//??
            output |= ((leftMouse == true ? 1 : 0) << 1);
            output |= ((keyUp == true ? 1 : 0) << 2);
            output |= ((keyLeft == true ? 1 : 0) << 3);
            output |= ((keyDown == true ? 1 : 0) << 4);
            output |= ((keyRight == true ? 1 : 0) << 5);
            //output |= ((godMode == true ? 1 : 0) << 6);//??
            //output |= ((suicide == true ? 1 : 0) << 7);
            output |= ((rightMouse == true ? 1 : 0) << 8);//??
                                                          //output |= ((levelUp == true ? 1 : 0) << 9);
            output |= ((gamepad == true ? 1 : 0) << 10);
            //output |= ((false == true ? 1 : 0) << 11);
            output |= ((switchClass == true ? 1 : 0) << 12);
            //output |= ((false == true ? 1 : 0) << 13);
            //output |= ((false == true ? 1 : 0) << 14);
            List<byte> bytes = new List<byte>();
            bytes.Add(1);
            bytes.AddRange(Writer.vu((uint)output));
            bytes.AddRange(Writer.vf((float)Math.Round(mouseX)));
            bytes.AddRange(Writer.vf((float)Math.Round(mouseY)));
            //Debug.WriteLine(keyUp + " " + keyLeft + " " + keyDown + " " + keyRight + " " + mouseY + " " + mouseY);
            switchClass = false;
            //switchClass = switchClass ? false : true;
            if (gamepad)
            {
                bytes.AddRange(Writer.vf(vx));
                bytes.AddRange(Writer.vf(vy));
            }
            //if (client_tick < (counter + 3000))
            //    return null;
            //if (client_tick < 1000)
            //    return null;
            //if ((player == null || player != null && player.x == player.previous_x && player.y == player.previous_y) && bytes.SequenceEqual(previous) && client_tick < (counter + 1))// < (counter + 20))
            //    return null;
            //else
            //{
            counter = client_tick;
            previous = bytes;
            return bytes.ToArray();
            //}
        }
    }
    public class Diep_Encryption
    {
        public abstract class PRNG//correct - types maybe wrong
        {
            public virtual int seed { get; set; }
            public virtual long seed2 { get; set; }
            public string variable { get; set; }
            public int index { get; set; }
            public abstract long next(bool first = true, bool last = false);
            public abstract void addoffset(int value);
        }
        public abstract class TripleSeedPRNG : PRNG
        {
            public int a_seed { get; set; }
            public int b_seed { get; set; }
            public int c_seed { get; set; }
            public string variable2 { get; set; }
            public string variable3 { get; set; }
            public int index2 { get; set; }
            public int index3 { get; set; }

            /*public override int seed
            {
                get { throw new Exception(); }
                set { throw new Exception(); }
            }
            public override long seed2
            {
                get { throw new Exception(); }
                set { throw new Exception(); }
            }*/
        }
        public class OneShiftAdd : PRNG
        {
            public int[] shift { get; set; }
            public bool[] add { get; set; }
            public int[] value { get; set; }

            public OneShiftAdd(int seed, int[] shift, bool[] add, int[] value)
            {
                this.seed = seed;
                this.shift = shift;
                this.add = add;
                this.value = value;
            }

            public override long next(bool first = true, bool last = false)
            {
                for (int i = 0; i < shift.Length; i++)
                {
                    if (i == 1 || i == 6)
                    {
                        if (add[i])
                            seed = (((int)((uint)seed >> shift[i])) + value[i]) ^ seed;
                        else
                            seed = (((int)((uint)seed >> shift[i])) | value[i]) ^ seed;
                    }
                    else if (add[i])
                        seed = ((seed << shift[i]) + value[i]) ^ seed;
                    else
                        seed = ((seed << shift[i]) | value[i]) ^ seed;
                }

                return seed;
            }
            public override void addoffset(int value)
            {
                seed += value;
            }
        }
        public class XorShift : PRNG//correct - types maybe wrong
        {
            public int[] values1 { get; set; }
            public int[] values2 { get; set; }
            public int extravalue { get; set; }

            public XorShift(int seed, int[] values1, int[] values2, int extravalue = 0)
            {
                this.seed = seed;
                this.values1 = values1;
                this.values2 = values2;
                this.extravalue = extravalue;
            }
            public override long next(bool first = true, bool last = false)
            {
                int value1 = 0;
                int value2 = 0;
                if ((values1.Length + values2.Length) % 2 == 1)
                    for (int i = 0; i < values1.Length + values2.Length; i++)
                    {
                        if (i % 2 == 0)
                            seed ^= (seed << values1[value1++]);
                        else
                            seed ^= (int)((uint)seed >> values2[value2++]);
                    }
                else
                {
                    if (!first)
                        seed ^= (seed << extravalue);
                    for (int i = 0; i < values1.Length + values2.Length; i++)
                    {
                        if (i % 2 == 0)
                            seed ^= (seed << values1[value1++]);
                        else
                            seed ^= (int)((uint)seed >> values2[value2++]);
                    }
                    if (last)
                        seed ^= (seed << extravalue);
                }
                return seed;
            }
            public override void addoffset(int value)
            {
                seed += value;
            }
        }
        public class TripleXorShift : TripleSeedPRNG
        {
            public int[] values1 { get; set; }
            public int[] values2 { get; set; }

            public TripleXorShift(int a_seed, int b_seed, int c_seed, int[] values1, int[] values2)
            {
                this.a_seed = a_seed;
                this.b_seed = b_seed;
                this.c_seed = c_seed;
                this.values1 = values1;
                this.values2 = values2;
            }

            public override long next(bool first = true, bool last = false)
            {
                int value1 = 0;
                int value2 = 0;
                a_seed ^= (a_seed << values1[value1++]);
                a_seed ^= (int)((uint)a_seed >> values2[value2++]);
                a_seed ^= (a_seed << values1[value1++]);

                b_seed ^= (b_seed << values1[value1++]);
                b_seed ^= (int)((uint)b_seed >> values2[value2++]);
                b_seed ^= (b_seed << values1[value1++]);

                c_seed ^= (c_seed << values1[value1++]);
                c_seed ^= (int)((uint)c_seed >> values2[value2++]);
                c_seed ^= (c_seed << values1[value1++]);

                return a_seed + b_seed + c_seed;
            }
            public override void addoffset(int value)
            {
                a_seed += value;
                b_seed += value;
                c_seed += value;
            }
        }
        public class LCG : PRNG//correct - types maybe wrong
        {
            public long mul { get; set; }
            public long add { get; set; }
            public ulong mod { get; set; }

            /*public override int seed
            {
                get { throw new Exception(); }
                set { throw new Exception(); }
            }*/
            public LCG(int seed, long mul, long add, long mod)
            {
                this.seed2 = seed;
                this.mul = mul;
                this.add = add;
                this.mod = (ulong)mod;
            }
            public override long next(bool first = true, bool last = false)
            {
                //Debug.WriteLine(seed);
                //Debug.WriteLine((ulong)(((ulong)(seed) * (ulong)this.mul + (ulong)this.add)));
                //ulong % ulong
                seed2 = (long)((ulong)((long)((uint)seed2 >> 0) * mul + add) % (ulong)mod);//correct
                                                                                           //Debug.WriteLine(seed);
                                                                                           // this.seed = Number(this.seed & 0xffffffffn) | 0;
                seed2 = (int)((seed2 & 0xFFFFFFFF) | 0);
                return seed2;// & 0xffffffff | 0;
            }
            public override void addoffset(int value)
            {
                seed2 = (int)(seed2 + value);
            }
        }
        public class TripleLCG : TripleSeedPRNG
        {
            public int a_mul { get; set; }
            public int a_add { get; set; }
            public int b_mul { get; set; }
            public int b_add { get; set; }
            public int c_mul { get; set; }
            public int c_add { get; set; }

            public TripleLCG(int a_seed, int a_mul, int a_add, int b_seed, int b_mul, int b_add, int c_seed, int c_mul, int c_add)
            {
                this.a_seed = a_seed;
                this.a_mul = a_mul;
                this.a_add = a_add;
                this.b_seed = b_seed;
                this.b_mul = b_mul;
                this.b_add = b_add;
                this.c_seed = c_seed;
                this.c_mul = c_mul;
                this.c_add = c_add;
            }
            public override long next(bool first = true, bool last = false)
            {
                a_seed = a_seed * a_mul + a_add;
                b_seed = b_seed * b_mul + b_add;
                c_seed = c_seed * c_mul + c_add;
                //Debug.WriteLine((int)a.seed + " " + (int)b.seed + " " + (int)c.seed);
                return c_seed + a_seed + b_seed;
            }
            public override void addoffset(int value)
            {
                a_seed += value;
                b_seed += value;
                c_seed += value;
            }
        }
        public class Cipher
        {
            //int type;
            //int[] seeds;
            //int[] s_seeds;
            //int[] c_seeds;
            int s_xorTableSize;
            PRNG s_headerSubstitutionCount;
            PRNG s_xorTableGenerator;
            PRNG s_xorTableShuffler;
            byte[] s_sBoxEncrypt = new byte[128];
            public object s_lock = new object();
            public bool[,] s_swaps = new bool[12, 12];
            int c_xorTableSize;
            PRNG c_headerSubstitutionCount;
            PRNG c_xorTableGenerator;
            PRNG c_xorTableShuffler;
            byte[] c_sBoxDecrypt = new byte[128];
            public object c_lock = new object();
            public bool[,] c_swaps = new bool[12, 12];
            public Cipher copy()
            {
                return new Cipher(s_xorTableSize, s_headerSubstitutionCount, s_xorTableGenerator, s_xorTableShuffler, s_sBoxEncrypt, c_xorTableSize, c_headerSubstitutionCount, c_xorTableGenerator, c_xorTableShuffler, c_sBoxDecrypt);
            }
            public void addoffset(int value)
            {
                c_headerSubstitutionCount.addoffset(value);
                c_xorTableGenerator.addoffset(value);
                c_xorTableShuffler.addoffset(value);
            }
            //public PRNG constructPrngFromConfig(dynamic config)
            //{
            //    if (config.name == "TripleLCG")
            //    {
            //        return new TripleLCG(
            //          new LCG(
            //            new Seed((uint)config.a.seed),
            //            (int)config.a.mul,
            //            (int)config.a.add,
            //            0
            //          ),
            //          new LCG(
            //            new Seed((uint)config.b.seed),
            //            (int)config.b.mul,
            //            (int)config.b.add,
            //            0
            //          ),
            //          new LCG(
            //            new Seed((uint)config.c.seed),
            //            (int)config.c.mul,
            //            (int)config.c.add,
            //            0
            //          )
            //        );
            //    }
            //
            //    if (config.name == "XorShift")
            //    {
            //        return new XorShift(
            //          new Seed((uint)config.seed, this.seeds, (bool)config.isOffset),
            //          (int)config.a,
            //          (int)config.b,
            //          (int)config.c
            //        );
            //    }
            //
            //    if (config.name == "LCG")
            //    {
            //        return new LCG(
            //          new Seed((uint)config.seed, this.seeds, (bool)config.isOffset),
            //          (int)config.mul,
            //          (int)config.add,
            //          (int)config.mod
            //        );
            //    }
            //    return null;
            //}
            PRNG GetFromJson(dynamic value)
            {
                JObject idk = value;
                var json = idk.ToString();
                if (json.Contains("\"shift\":"))
                {
                    return idk.ToObject<OneShiftAdd>();
                    //return new OneShiftAdd((int)value["seed"], ((JArray)value["shift"]).Select(x => (int)x).ToArray(), ((JArray)value["add"]).Select(x => (bool)x).ToArray(), ((JArray)value["value"]).Select(x => (int)x).ToArray());
                }
                if (json.Contains("\"values1\":") && json.Contains("\"a_seed\":"))
                {
                    return idk.ToObject<TripleXorShift>();
                    //return new XorShift((int)value["seed"], ((JArray)value["values1"]).Select(x => (int)x).ToArray(), ((JArray)value["values2"]).Select(x => (int)x).ToArray());
                }
                if (json.Contains("\"values1\":"))
                {
                    return idk.ToObject<XorShift>();
                    //return new XorShift((int)value["seed"], ((JArray)value["values1"]).Select(x => (int)x).ToArray(), ((JArray)value["values2"]).Select(x => (int)x).ToArray());
                }
                if (json.Contains("\"a_seed\":"))
                {
                    return idk.ToObject<TripleLCG>();
                    //return new TripleLCG((int)value["a_seed"], (int)value["a_mul"], (int)value["a_add"], (int)value["b_seed"], (int)value["b_mul"], (int)value["b_add"], (int)value["c_seed"], (int)value["c_mul"], (int)value["c_add"]);
                }
                if (json.Contains("\"mul\":"))
                {
                    return idk.ToObject<LCG>();
                    //return new LCG((int)value["seed2"], (long)value["mul"], (long)value["add"], (long)value["mod"]);
                }
                Debugger.Break();
                return null;
            }
            public Cipher()
            {
                dynamic temp = JObject.Parse(File.ReadAllText(INFO.headless_config_file));
                s_headerSubstitutionCount = GetFromJson(temp.s_headerSubstitutionCount);
                s_xorTableGenerator = GetFromJson(temp.s_xorTableGenerator);
                s_xorTableShuffler = GetFromJson(temp.s_xorTableShuffler);
                s_xorTableSize = temp.s_xorTableSize;
                s_sBoxEncrypt = temp.s_sBoxEncrypt;

                c_headerSubstitutionCount = GetFromJson(temp.c_headerSubstitutionCount);
                c_xorTableGenerator = GetFromJson(temp.c_xorTableGenerator);
                c_xorTableShuffler = GetFromJson(temp.c_xorTableShuffler);
                c_xorTableSize = temp.c_xorTableSize;
                c_sBoxDecrypt = temp.c_sBoxDecrypt;
                //s_headerSubstitutionCount = new TripleLCG(200694476, 179, 217, 326675899, 174, 139, 1198164122, 178, 112);
                //s_xorTableGenerator = new LCG(0, 91615, 973, 1645252603);
                //s_xorTableShuffler = new LCG(336719209, 57946, 1519, 1770985414);
                //s_xorTableSize = 19;
                //s_sBoxEncrypt = new byte[] { 0, 7, 9, 51, 21, 3, 97, 15, 86, 125, 92, 73, 65, 29, 23, 71, 99, 94, 113, 49, 35, 39, 24, 68, 18, 82, 28, 41, 33, 11, 63, 67, 64, 48, 52, 27, 55, 5, 75, 16, 112, 81, 111, 107, 47, 1, 77, 118, 56, 4, 59, 37, 88, 19, 78, 69, 72, 25, 109, 103, 80, 85, 32, 40, 108, 123, 30, 83, 120, 79, 95, 43, 100, 53, 84, 20, 34, 98, 105, 61, 74, 89, 60, 31, 117, 126, 90, 87, 115, 127, 93, 17, 101, 106, 121, 8, 10, 46, 119, 91, 116, 54, 96, 58, 66, 70, 44, 45, 102, 36, 76, 6, 26, 50, 12, 104, 124, 14, 122, 114, 2, 62, 38, 22, 42, 13, 110, 57 };
                //
                //c_headerSubstitutionCount = new TripleLCG(570443443, 177, 112, 878565407, 173, 28, 1331293646, 180, 217);
                //c_xorTableGenerator = new XorShift(1403178637, 14, 15, 5);
                //c_xorTableShuffler = new TripleLCG(154672434, 178, 112, 1403178637, 174, 112, 1005731752, 172, 217);
                //c_xorTableSize = 19;
                //c_sBoxDecrypt = new byte[] { 19, 1, 115, 112, 67, 40, 92, 12, 86, 97, 91, 36, 47, 43, 2, 29, 111, 80, 8, 6, 11, 74, 46, 122, 127, 100, 31, 82, 87, 116, 37, 24, 50, 77, 108, 105, 59, 44, 73, 101, 41, 79, 52, 26, 110, 22, 95, 85, 103, 9, 64, 63, 114, 42, 106, 96, 126, 30, 88, 83, 118, 75, 32, 14, 23, 49, 120, 81, 13, 68, 55, 72, 61, 56, 5, 107, 0, 25, 124, 94, 66, 102, 53, 117, 3, 60, 38, 78, 62, 20, 98, 57, 71, 17, 51, 18, 21, 125, 35, 99, 28, 69, 90, 27, 70, 39, 15, 34, 4, 89, 109, 121, 84, 76, 58, 16, 54, 123, 65, 104, 45, 7, 113, 93, 48, 119, 33, 10 };
            }

            public Cipher(int xorTableSize, PRNG headerSubstitutionCount, PRNG xorTableGenerator, PRNG xorTableShuffler, byte[] sBoxEncrypt, int c_xorTableSize, PRNG c_headerSubstitutionCount, PRNG c_xorTableGenerator, PRNG c_xorTableShuffler, byte[] c_sBoxDecrypt)
            {
                s_xorTableSize = xorTableSize;
                s_headerSubstitutionCount = headerSubstitutionCount;
                s_xorTableGenerator = xorTableGenerator;
                s_xorTableShuffler = xorTableShuffler;
                s_sBoxEncrypt = sBoxEncrypt;
                this.c_xorTableSize = c_xorTableSize;
                this.c_headerSubstitutionCount = c_headerSubstitutionCount;
                this.c_xorTableGenerator = c_xorTableGenerator;
                this.c_xorTableShuffler = c_xorTableShuffler;
                this.c_sBoxDecrypt = c_sBoxDecrypt;
            }

            public byte[] Encrypt(byte[] buffer)//correct
            {
                byte[] shuffled = new byte[buffer.Length];

                shuffled[0] = shuffleHeader(buffer[0]);

                var xorTable = s_generateXorTable();

                for (var i = 1; i < buffer.Length; i++)
                {
                    shuffled[i] = (byte)(buffer[i] ^ xorTable[i % xorTable.Length]);
                }

                return shuffled;
            }
            public byte[] Decrypt(byte[] buffer)//correct
            {
                byte[] unshuffled = new byte[buffer.Length];
                unshuffled[0] = unshuffleHeader(buffer[0]);

                var xorTable = c_generateXorTable();

                for (var i = 1; i < buffer.Length; i++)
                {
                    unshuffled[i] = (byte)(buffer[i] ^ xorTable[i % xorTable.Length]);
                }

                return unshuffled;
            }
            public byte[] c_generateXorTable()//correct
            {
                SetPRGNseed(c_headerSubstitutionCount, c_xorTableGenerator);
                SetPRGNseed(c_headerSubstitutionCount, c_xorTableShuffler);

                var xorTable = new byte[this.c_xorTableSize].ToList().Select((x, i) => (byte)this.c_xorTableGenerator.next(i == 0, i == c_xorTableSize - 1)).ToArray();

                SetPRGNseed(c_xorTableGenerator, c_xorTableShuffler);

                for (var i = this.c_xorTableSize - 1; i > 0; i--)
                {
                    var index = ((uint)this.c_xorTableShuffler.next()) % (uint)(i + 1);
                    var temp = xorTable[i];
                    xorTable[i] = xorTable[index];
                    xorTable[index] = temp;
                }
                //setseed(c_xorTableGenerator, getseed(c_xorTableGenerator) ^ ((getseed(c_xorTableGenerator) << 13) | 0) | 0);
                SetPRGNseed(c_xorTableShuffler, c_xorTableGenerator);

                SetPRGNseed(c_xorTableGenerator, c_headerSubstitutionCount);
                SetPRGNseed(c_xorTableShuffler, c_headerSubstitutionCount);
                return xorTable;
            }
            public byte[] s_generateXorTable()//correct
            {
                SetPRGNseed(s_headerSubstitutionCount, s_xorTableGenerator);
                SetPRGNseed(s_headerSubstitutionCount, s_xorTableShuffler);

                var xorTable = new byte[this.s_xorTableSize].ToList().Select((x, i) => (byte)this.s_xorTableGenerator.next(i == 0, i == s_xorTableSize - 1)).ToArray();

                SetPRGNseed(s_xorTableGenerator, s_xorTableShuffler);

                for (var i = this.s_xorTableSize - 1; i > 0; i--)
                {
                    var index = ((uint)s_xorTableShuffler.next()) % (uint)(i + 1);
                    var temp = xorTable[i];
                    xorTable[i] = xorTable[index];
                    xorTable[index] = temp;
                }

                SetPRGNseed(s_xorTableShuffler, s_xorTableGenerator);

                SetPRGNseed(s_xorTableGenerator, s_headerSubstitutionCount);
                SetPRGNseed(s_xorTableShuffler, s_headerSubstitutionCount);
                return xorTable;
            }
            public byte shuffleHeader(byte header)//working
            {
                var random = s_headerSubstitutionCount.next();
                //Debug.WriteLine(random);
                var substitutionCount = random & 15;
                for (var i = 0; i <= substitutionCount; i++)
                {
                    //header = header & 255;
                    //var num = base + (header * 3) + offset
                    //header = HEAPU8[num + 2] ^ 126
                    header = s_sBoxEncrypt[header];
                }

                return header;
            }
            public byte unshuffleHeader(byte header)//correct
            {
                var random = this.c_headerSubstitutionCount.next();
                //Debug.WriteLine("random " + random);
                var substitutionCount = random & 15;
                for (var i = 0; i <= substitutionCount; i++)//94
                {
                    header = this.c_sBoxDecrypt[header];//51
                }

                return header;
            }
            public void SetPRGNseed(PRNG from, PRNG to, bool save = false)
            {
                /* 1->1
                 * 
                 * 2->1
                 * 3->1
                 * 
                 * 1->2
                 * 1->3
                 * 
                 * 2->2
                 * 3->2
                 * 2->3
                 * 3->3
                 */
                if (from.index == to.index && from.variable == to.variable)
                {
                    //Debug.WriteLine("1->1");
                    setseed(to, getseed(from));
                }
                if (from.GetType().IsSubclassOf(typeof(TripleSeedPRNG)))
                {
                    if (((TripleSeedPRNG)from).index2 == to.index && ((TripleSeedPRNG)from).variable2 == to.variable)
                    {
                        //Debug.WriteLine("2->1");
                        setseed(to, getseed(from, 2));
                    }
                    if (((TripleSeedPRNG)from).index3 == to.index && ((TripleSeedPRNG)from).variable3 == to.variable)
                    {
                        //Debug.WriteLine("3->1");
                        setseed(to, getseed(from, 3));
                    }
                }
                if (to.GetType().IsSubclassOf(typeof(TripleSeedPRNG)))
                {
                    if (from.index == ((TripleSeedPRNG)to).index2 && from.variable == ((TripleSeedPRNG)to).variable2)
                    {
                        //Debug.WriteLine("1->2");
                        setseed(to, getseed(from), 2);
                    }
                    if (from.index == ((TripleSeedPRNG)to).index3 && from.variable == ((TripleSeedPRNG)to).variable3)
                    {
                        //Debug.WriteLine("1->3");
                        setseed(to, getseed(from), 3);
                    }
                }
                if (from.GetType().IsSubclassOf(typeof(TripleSeedPRNG)) && to.GetType().IsSubclassOf(typeof(TripleSeedPRNG)))
                {
                    if (((TripleSeedPRNG)from).index2 == ((TripleSeedPRNG)to).index2 && ((TripleSeedPRNG)from).variable2 == ((TripleSeedPRNG)to).variable2)
                    {
                        setseed(to, getseed(from, 2), 2);
                    }
                    if (((TripleSeedPRNG)from).index3 == ((TripleSeedPRNG)to).index2 && ((TripleSeedPRNG)from).variable3 == ((TripleSeedPRNG)to).variable2)
                    {
                        setseed(to, getseed(from, 3), 2);
                    }
                    if (((TripleSeedPRNG)from).index2 == ((TripleSeedPRNG)to).index3 && ((TripleSeedPRNG)from).variable2 == ((TripleSeedPRNG)to).variable3)
                    {
                        setseed(to, getseed(from, 2), 3);
                    }
                    if (((TripleSeedPRNG)from).index3 == ((TripleSeedPRNG)to).index3 && ((TripleSeedPRNG)from).variable3 == ((TripleSeedPRNG)to).variable3)
                    {
                        setseed(to, getseed(from, 3), 3);
                    }
                }
            }
            public void setseed(PRNG pRNG, int value, int i = 1)
            {
                if (pRNG.GetType() == typeof(LCG))
                    pRNG.seed2 = value;
                else if (pRNG.GetType().IsSubclassOf(typeof(TripleSeedPRNG)))
                {
                    if (i == 1)
                        ((TripleSeedPRNG)pRNG).a_seed = value;
                    if (i == 2)
                        ((TripleSeedPRNG)pRNG).b_seed = value;
                    if (i == 3)
                        ((TripleSeedPRNG)pRNG).c_seed = value;
                }
                else
                    pRNG.seed = value;
            }
            public int getseed(PRNG pRNG, int i = 1)
            {
                if (pRNG.GetType() == typeof(LCG))
                    return (int)pRNG.seed2;
                if (pRNG.GetType().IsSubclassOf(typeof(TripleSeedPRNG)))
                {
                    if (i == 1)
                        return ((TripleSeedPRNG)pRNG).a_seed;
                    if (i == 2)
                        return ((TripleSeedPRNG)pRNG).b_seed;
                    if (i == 3)
                        return ((TripleSeedPRNG)pRNG).c_seed;
                }
                return pRNG.seed;
            }
        }
    }
    class Writer
    {
        public static byte[] cstr(string str)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(str);
            buffer = buffer.Concat(new byte[] { 0 }).ToArray();

            return buffer;
        }
        public static byte[] purestring(string str)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(str);

            return buffer;
        }
        public static byte[] vu(ulong num)
        {
            List<byte> buffer = new List<byte>();
            do
            {
                ulong part = num;

                num >>= 7;

                if (num != 0) part |= 0x80;
                buffer.Add((byte)part);

            } while (num != 0);

            return buffer.ToArray();
        }
        public static byte[] vi(long num)
        {
            int sign = (int)((uint)(num & 0x80000000) >> 31);

            if (sign != 0) num = ~num;

            long part = (num << 1) | sign;

            return vu((ulong)part);

        }
        public static byte[] vf(float num)
        {
            var val = BitConverter.ToInt32(BitConverter.GetBytes(num), 0);
            return vi(((val & 0xff) << 24) | ((val & 0xff00) << 8) | ((val >> 8) & 0xff00) | ((val >> 24) & 0xff));

        }
    }
}
