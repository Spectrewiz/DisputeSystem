using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using System.Net;

namespace DisputeSystem
{
    [APIVersion(1, 11)]
    public class DisputeSystem : TerrariaPlugin
    {
        public static Config Config { get; set; }
        internal static string ConfigPath { get { return Path.Combine(TShock.SavePath, @"DisputeSystem\DisputeConfig.json"); } }
        public static string Banned = "";
        public static string StandardDispute = "";
        public static string downloadFromUpdate;
        public static string versionFromUpdate;
        public static int update = 0;
        public static string[] readStandardDispute = new string[] { "" };
        public static List<Player> Players = new List<Player>();
        public static DateTime lastupdate;
        public static DateTime lastupdatecheck;
        public override string Name
        {
            get { return "Dispute System"; }
        }

        public override string Author
        {
            get { return "Spectrewiz"; }
        }

        public override string Description
        {
            get { return "Banned players can file disputes against their bans."; }
        }

        public override Version Version
        {
            get { return new Version(0, 9, 0); }
        }

        public override void Initialize()
        {
            GameHooks.Update += OnUpdate;
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
            }
            base.Dispose(disposing);
        }

        public DisputeSystem(Main game)
            : base(game)
        {
            Config = new Config();
            Order = -1;
        }

        public void OnInitialize()
        {
            bool dis = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("say"))
                        dis = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!dis)
                permlist.Add("Dispute");
            TShock.Groups.AddPermissions("trustedadmin", permlist);

            Commands.ChatCommands.Add(new Command("Dispute", Ban, "dban"));
            Commands.ChatCommands.Add(new Command(Dispute, "dispute", "disputes"));
            Banned = Path.Combine(TShock.SavePath, @"DisputeSystem\Banned.txt");
            StandardDispute = Path.Combine(TShock.SavePath, @"DisputeSystem\StandardDispute.txt");

            if (!Directory.Exists(Path.Combine(TShock.SavePath, "DisputeSystem")))
            {
                Directory.CreateDirectory(Path.Combine(TShock.SavePath, "DisputeSystem"));
                Directory.CreateDirectory(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes"));
            }
            try
            {
                if (File.Exists(ConfigPath))
                    Config = Config.Read(ConfigPath);
                Config.Write(ConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in Dispute System config file");
                Console.ResetColor();
                Log.Error("-------- Config Exception in DisputeSystem Config file (DisputeConfig.json) --------");
                Log.Error(ex.ToString());
                Log.Error("------------------------------------ Error End ------------------------------------");
            }
            try
            {
                if (!File.Exists(StandardDispute))
                {
                    StreamWriter writer = new StreamWriter(StandardDispute, true);
                    readStandardDispute = new WebClient().DownloadString("https://github.com/Spectrewiz/DisputeSystem/raw/master/StandardDispute.txt").Split('\n');
                    for (int i = 0; i < readStandardDispute.Length; i++)
                    {
                        writer.WriteLine(readStandardDispute[i]);
                    }
                    writer.Close();
                }
                else
                    readStandardDispute = File.ReadAllText(StandardDispute).Split('\n');
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error while setting up DisputeSystem StandardDispute.txt file");
                Console.ResetColor();
                Log.Error("-------- Web Exception in DisputeSystem StandardDispute.txt file --------");
                Log.Error(ex.ToString());
                Log.Error("------------------------------- Error End -------------------------------");
            }
            lastupdate = DateTime.Now;
        }

        public void OnUpdate()
        {
            if ((DateTime.Now - lastupdate).TotalSeconds >= 5)
            {
                lastupdate = DateTime.Now;
                lock (Players)
                {
                    foreach (Player player in Players)
                    {
                        if (player.GetBan() == Player.Banned.banned)
                            player.TSPlayer.Disable();
                    }
                }
            }

            if (update == 0)
            {
                if (UpdateChecker())
                    update++;
                else
                    update--;
            }
            else if (update < 0)
            {
                if ((DateTime.Now - lastupdatecheck).TotalHours >= 3)
                {
                    if (UpdateChecker())
                        update = 1;
                    else
                        lastupdatecheck = DateTime.Now;
                }
            }
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(who));
            string name = TShock.Players[who].Name.ToLower();
            string line;
            var ListedPlayer = Player.GetPlayerByName(name);
            if (File.Exists(Banned))
            {
                using (StreamReader reader = new StreamReader(Banned))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (String.Compare(line, name) == 0)
                        {
                            ListedPlayer.Ban(Player.Banned.banned);
                            ListedPlayer.TSPlayer.Teleport((int)TShock.Warps.FindWarp(Config.Warp).WarpPos.X, (int)TShock.Warps.FindWarp(Config.Warp).WarpPos.Y);
                        }
                    }
                }
            }
            if (TShock.Players[who].Group.Name.ToLower() == "superadmin")
                if (update > 0)
                {
                    TShock.Players[who].SendMessage("Update for Dispute System available! Check log for download link.", Color.Yellow);
                    Log.Info(string.Format("NEW VERSION: {0}  |  Download here: {1}", versionFromUpdate, downloadFromUpdate));
                }
        }

        public void OnLeave(int ply)
        {
            lock (Players)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                    {
                        Players.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
            string name = TShock.Players[ply].Name.ToLower();
            var ListedPlayer = Player.GetPlayerByName(name);
            if (ListedPlayer.GetBan() == Player.Banned.banned)
            {
                if (text.Trim().StartsWith("/"))
                {
                    string[] textsplit = text.Trim().ToLower().Split(' ');
                    if (textsplit[0] == "/dispute")
                    {
                        return;
                    }
                    else
                    {
                        TShock.Players[ply].SendMessage("Banned people cannot execute commands.", Color.Red);
                        TShock.Players[ply].SendMessage("If you wish to dispute your ban, type \"/dispute\".", Color.Red);
                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    TShock.Players[ply].SendMessage("Banned people cannot talk.", Color.Red);
                    TShock.Players[ply].SendMessage("If you wish to dispute your ban, type \"/dispute\".", Color.Red);
                    e.Handled = true;
                    return;
                }
            }
            return;
        }

        public static void Ban(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendMessage("Syntax: /dban <playername>", 30, 144, 255);
                args.Player.SendMessage("This toggles whether or not the player is banned.", 135, 206, 255);
            }
            else
            {
                var player = TShock.Utils.FindPlayer(args.Parameters[0]);
                if (player.Count == 0)
                {
                    args.Player.SendMessage("Invalid player!", Color.Red);
                    return;
                }
                else if (player.Count > 1)
                {
                    args.Player.SendMessage(string.Format("More than one ({0}) player matched!", player.Count), Color.Red);
                    return;
                }
                var ListedPlayer = Player.GetPlayerByName(player[0].Name);
                if (ListedPlayer.GetBan() == Player.Banned.free)
                {
                    try
                    {
                        ListedPlayer.Ban(Player.Banned.banned);
                        ListedPlayer.TSPlayer.Teleport((int)TShock.Warps.FindWarp(Config.Warp).WarpPos.X, (int)TShock.Warps.FindWarp(Config.Warp).WarpPos.Y);
                        using (StreamWriter writer = new StreamWriter(Banned))
                        {
                            writer.WriteLine(player[0].Name.ToLower());
                        }
                    }
                    catch (Exception e) { Log.Error(e.Message); args.Player.SendMessage("Error, check logs for details.", Color.Red); }
                    finally { args.Player.SendMessage("Player " + player[0].Name + " is banned!", 30, 144, 255); ListedPlayer.TSPlayer.SendMessage("You have been banned by " + args.Player.Name + ". To dispute your ban, type \"/dispute\"", Color.Red); }
                }
                else
                {
                    try
                    {
                        ListedPlayer.Ban(Player.Banned.free);
                        ListedPlayer.TSPlayer.Teleport(Main.spawnTileX, Main.spawnTileY);
                        string line = null;
                        string line_to_delete = args.Parameters[0];

                        StreamReader reader = new StreamReader(Banned);
                        StreamWriter writer = new StreamWriter("tempd.txt", true);
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (String.Compare(line, line_to_delete, true) != 0)
                            {
                                writer.WriteLine(line);
                            }
                        }
                        reader.Close();
                        writer.Close();
                        File.Delete(Banned);
                        File.Move("tempd.txt", Banned);
                        File.Delete(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes\" + ListedPlayer.TSPlayer.Name + ".txt"));
                    }
                    catch (Exception e) { Log.Error(e.Message); }
                    finally { args.Player.SendMessage("Player " + player[0].Name + " is unbanned!", 30, 144, 255); ListedPlayer.TSPlayer.SendMessage("You have been unbanned by " + args.Player.Name + ". In the future, play maturely.", 30, 144, 255); ListedPlayer.TSPlayer.SendMessage("If you misbehave again, it could result in a permanent ban.", Color.Red); }
                }
            }
        }

        public static void Dispute(CommandArgs args)
        {
            var FindMe = Player.GetPlayerByName(args.Player.Name);
            if (args.Parameters.Count < 1)
            {
                if (FindMe.GetBan() == Player.Banned.banned)
                {
                    for (int i = 0; i < readStandardDispute.Length; i++)
                    {
                        if (readStandardDispute[i].Contains("|"))
                        {
                            int r = Convert.ToInt32(readStandardDispute[i].Trim().Split('|')[1].Split(',')[0]);
                            int g = Convert.ToInt32(readStandardDispute[i].Trim().Split('|')[1].Split(',')[1]);
                            int b = Convert.ToInt32(readStandardDispute[i].Trim().Split('|')[1].Split(',')[2]);
                            Color rbg = new Color(r, b, g);
                            args.Player.SendMessage(readStandardDispute[i].Trim().Split('|')[0], rbg);
                        }
                        else
                        {
                            args.Player.SendMessage(readStandardDispute[i].Trim(), 30, 144, 255);
                        }
                    }
                }
                else if (args.Player.Group.HasPermission("Dispute"))
                {
                    args.Player.SendMessage("Syntax: /Dispute <list/read> <playername>", 30, 144, 255);
                }
            }
            else
            {
                if (FindMe.GetBan() == Player.Banned.banned)
                {
                    if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "read")
                    {
                        string filetext = File.ReadAllText(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes\" + args.Player.Name + @".txt"));
                        for (int i = 0; i < filetext.Split('\n').Length; i++)
                        {
                            args.Player.SendMessage(filetext.Split('\n')[i], 30, 144, 255);
                        }
                    }
                    else
                    {
                        try
                        {
                            string text = "";
                            foreach (string word in args.Parameters)
                            {
                                text = text + word + " ";
                            }

                            StreamWriter sw = new StreamWriter(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes\" + args.Player.Name + @".txt"), true);
                            sw.WriteLine(text.Trim());
                            sw.Close();

                            args.Player.SendMessage("Your Dispute has been updated!", 30, 144, 255);
                        }
                        catch (Exception e)
                        {
                            args.Player.SendMessage("Your dispute could not be sent, contact an administrator.", Color.Red);
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(e.Message);
                            Console.ResetColor();
                            Log.Error(e.Message);
                        }
                    }
                }
                else if (args.Player.Group.HasPermission("Dispute"))
                {
                    switch (args.Parameters[0].ToLower())
                    {
                        case "list":
                            List<string> listfilepaths = new List<string>();
                            foreach (string filename in Directory.GetFiles(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes"), "*.txt"))
                            {
                                listfilepaths.Add(filename);
                            }
                            if (listfilepaths.Count > 0)
                            {
                                args.Player.SendMessage("--- List of Disputes Filed ---", 30, 144, 255);
                                for (int i = 0; i < listfilepaths.Count; i++)
                                {
                                    args.Player.SendMessage(listfilepaths[i].Split('\\')[listfilepaths[i].Split('\\').Length - 1].Split('.')[0], 135, 206, 255);
                                }
                            }
                            else
                            {
                                args.Player.SendMessage("There are no disputes filed.", Color.Red);
                            }
                            break;
                        case "read":
                            string filetext = "";
                            try
                            {
                                filetext = File.ReadAllText(Path.Combine(TShock.SavePath, @"DisputeSystem\Disputes\" + args.Parameters[1] + ".txt"));
                            }
                            catch (Exception e) { Log.Error(e.Message); args.Player.SendMessage("Cannot find file, please type the EXACT player name.", Color.Red); return; }
                            for (int i = 0; i < filetext.Split('\n').Length; i++)
                            {
                                args.Player.SendMessage(filetext.Split('\n')[i], 30, 144, 255);
                            }
                            break;
                        default:
                            args.Player.SendMessage("Syntax: /Dispute <list/read> <playername>", Color.Red);
                            break;
                    }
                }
            }
        }

        public bool UpdateChecker()
        {
            string raw;
            try
            {
                raw = new WebClient().DownloadString("https://github.com/Spectrewiz/DisputeSystem/raw/master/README.txt");
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                return false;
            }
            string[] readme = raw.Split('\n');
            string[] download = readme[readme.Length - 1].Split('-');
            Version version;
            if (!Version.TryParse(readme[0], out version)) return false;
            if (Version.CompareTo(version) >= 0) return false;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("New Dispute System version: " + readme[0].Trim());
            Console.WriteLine("Download here: " + download[1].Trim());
            Console.ResetColor();
            Log.Info(string.Format("NEW VERSION: {0}  |  Download here: {1}", readme[0].Trim(), download[1].Trim()));
            downloadFromUpdate = download[1].Trim();
            versionFromUpdate = readme[0].Trim();
            return true;
        }
    }
}