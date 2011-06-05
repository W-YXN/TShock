﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaAPI;
using TerrariaAPI.Hooks;

namespace TShockAPI
{
    public class TShock : TerrariaPlugin
    {
        public static TSPlayer[] players = new TSPlayer[Main.maxPlayers];

        public static string saveDir = "./tshock/";

        public static Version VersionNum = new Version(1, 8, 0, 0);

        public static string VersionCodename = "Fuck the wiki!";

        public static bool shownVersion = false;

        static bool[] BlacklistTiles;

        public override Version Version
        {
            get { return VersionNum; }
        }

        public override Version APIVersion
        {
            get { return new Version(1, 1); }
        }

        public override string Name
        {
            get { return "TShock"; }
        }

        public override string Author
        {
            get { return "The TShock Team"; }
        }

        public override string Description
        {
            get { return "The administration modification of the future."; }
        }

        static TShock()
        {
            //Tools.LoadGroups();

            #region Blacklisted tiles

            BlacklistTiles = new bool[0x80];
            BlacklistTiles[0] = true;
            BlacklistTiles[1] = true;
            BlacklistTiles[2] = true;
            BlacklistTiles[6] = true;
            BlacklistTiles[7] = true;
            BlacklistTiles[8] = true;
            BlacklistTiles[9] = true;
            BlacklistTiles[22] = true;
            BlacklistTiles[23] = true;
            BlacklistTiles[25] = true;
            BlacklistTiles[30] = true;
            BlacklistTiles[37] = true;
            BlacklistTiles[38] = true;
            BlacklistTiles[39] = true;
            BlacklistTiles[40] = true;
            BlacklistTiles[41] = true;
            BlacklistTiles[43] = true;
            BlacklistTiles[44] = true;
            BlacklistTiles[45] = true;
            BlacklistTiles[46] = true;
            BlacklistTiles[47] = true;
            BlacklistTiles[53] = true;
            BlacklistTiles[54] = true;
            BlacklistTiles[56] = true;
            BlacklistTiles[57] = true;
            BlacklistTiles[58] = true;
            BlacklistTiles[59] = true;
            BlacklistTiles[60] = true;
            BlacklistTiles[63] = true;
            BlacklistTiles[64] = true;
            BlacklistTiles[65] = true;
            BlacklistTiles[66] = true;
            BlacklistTiles[67] = true;
            BlacklistTiles[68] = true;
            BlacklistTiles[70] = true;
            BlacklistTiles[75] = true;
            BlacklistTiles[76] = true;

            #endregion Blacklisted tiles
        }

        public TShock(Main game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            try
            {
                FileTools.SetupConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Console.WriteLine("TShock Version " + Version.Major + "." + Version.Minor + "." + Version.Build + "." + Version.Revision + " (" + VersionCodename + ") now running.");
            Log.Initialize(FileTools.SaveDir + "log.txt", LogLevel.All, true);
            Log.Info("Starting...");
            GameHooks.OnPreInitialize += OnPreInit;
            GameHooks.OnPostInitialize += OnPostInit;
            GameHooks.OnUpdate += new Action<Microsoft.Xna.Framework.GameTime>(OnUpdate);
            GameHooks.OnLoadContent += new Action<Microsoft.Xna.Framework.Content.ContentManager>(OnLoadContent);
            ServerHooks.OnChat += new Action<int, string, HandledEventArgs>(OnChat);
            ServerHooks.OnJoin += new Action<int, AllowEventArgs>(OnJoin);
            NetHooks.OnPreGetData += GetData;
            NetHooks.OnGreetPlayer += new NetHooks.GreetPlayerD(OnGreetPlayer);
            NpcHooks.OnStrikeNpc += new NpcHooks.StrikeNpcD(NpcHooks_OnStrikeNpc);
            ServerHooks.OnCommand += new ServerHooks.CommandD(ServerHooks_OnCommand);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Log.Info("Hooks initialized");
            Commands.InitCommands();
            Log.Info("Commands initialized");
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (Main.worldPathName != null)
            {
                Main.worldPathName += ".crash";
                WorldGen.saveWorld();
            }
            Log.Error(e.ExceptionObject.ToString());
        }

        void ServerHooks_OnCommand(string cmd, HandledEventArgs e)
        {
        }

        public override void DeInitialize()
        {
            GameHooks.OnPreInitialize -= OnPreInit;
            GameHooks.OnPostInitialize -= OnPostInit;
            GameHooks.OnUpdate -= new Action<Microsoft.Xna.Framework.GameTime>(OnUpdate);
            GameHooks.OnLoadContent -= new Action<Microsoft.Xna.Framework.Content.ContentManager>(OnLoadContent);
            ServerHooks.OnChat -= new Action<int, string, HandledEventArgs>(OnChat);
            ServerHooks.OnJoin -= new Action<int, AllowEventArgs>(OnJoin);
            ServerHooks.OnCommand -= new ServerHooks.CommandD(ServerHooks_OnCommand);
            NetHooks.OnPreGetData -= GetData;
            NetHooks.OnGreetPlayer -= new NetHooks.GreetPlayerD(OnGreetPlayer);
            NpcHooks.OnStrikeNpc -= new NpcHooks.StrikeNpcD(NpcHooks_OnStrikeNpc);
        }

        /*
         * Hooks:
         * */

        void NpcHooks_OnStrikeNpc(NpcStrikeEventArgs e)
        {
            if (ConfigurationManager.infiniteInvasion)
            {
                IncrementKills();
                if (Main.invasionSize < 10)
                {
                    Main.invasionSize = 20000000;
                }
            }
        }

        void GetData(GetDataEventArgs e)
        {
            if (Main.netMode != 2) { return; }
            if (e.MsgID == 17)
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    byte type = br.ReadByte();
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    byte typetile = br.ReadByte();
                    if (type == 0 || type == 1)
                        if (ConfigurationManager.spawnProtect)
                            if (!players[e.Msg.whoAmI].group.HasPermission("editspawn"))
                            {
                                var flag = CheckSpawn(x, y);
                                if (flag)
                                {
                                    Tools.SendMessage(e.Msg.whoAmI, "The spawn is protected!", new float[] { 255f, 0f, 0f });
                                    e.Handled = true;
                                }
                            }

                    if (type == 0 && BlacklistTiles[Main.tile[x, y].type] && Main.player[e.Msg.whoAmI].active)
                    {
                        players[e.Msg.whoAmI].tileThreshold++;
                        players[e.Msg.whoAmI].tilesDestroyed.Add(new Position((float)x, (float)y), Main.tile[x, y]);
                    }
                    return;
                }
            }
            else if (e.MsgID == 0x1e)
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    int id = br.ReadByte();
                    bool pvp = br.ReadBoolean();

                    Main.player[e.Msg.whoAmI].hostile = pvp;
                    if (id != e.Msg.whoAmI)
                        Main.player[e.Msg.whoAmI].hostile = true;
                    if (ConfigurationManager.permaPvp)
                        Main.player[e.Msg.whoAmI].hostile = true;
                    NetMessage.SendData(30, -1, -1, "", e.Msg.whoAmI);
                    e.Handled = true;
                    return;
                }
            }
            else if (e.MsgID == 0x0A) //SendSection
            {
                Tools.Broadcast(string.Format("{0}({1}) attempted sending a section", Main.player[e.Msg.whoAmI].name, e.Msg.whoAmI));
                Tools.Kick(e.Msg.whoAmI, "SendSection abuse.");
                e.Handled = true;
            }
            else if (e.MsgID == 0x17) //Npc Data
            {
                Tools.Broadcast(string.Format("{0}({1}) attempted spawning an NPC", Main.player[e.Msg.whoAmI].name, e.Msg.whoAmI));
                Tools.Kick(e.Msg.whoAmI, "Spawn NPC abuse");
                e.Handled = true;
            }
            else if (e.MsgID == 0x0D) //Update Player
            {
                byte plr = e.Msg.readBuffer[e.Index];
                if (plr != e.Msg.whoAmI)
                {
                    Tools.Kick(e.Msg.whoAmI, "Update Player abuse");
                    e.Handled = true;
                }
            }
            else if (e.MsgID == 0x10)
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    byte ply = br.ReadByte();
                    short life = br.ReadInt16();
                    short maxLife = br.ReadInt16();

                    if (maxLife > Main.player[ply].statLifeMax + 20 || life > maxLife)
                    {
                        if (players[ply].syncHP)
                        {
                            if (maxLife > Main.player[ply].statLifeMax + 20 || life > maxLife)
                                Tools.HandleCheater(ply);
                        }
                        else
                        {
                            players[ply].syncHP = true;
                        }
                    }
                }
            }
            else if (e.MsgID == 0x2a)
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    byte ply = br.ReadByte();
                    short mana = br.ReadInt16();
                    short maxmana = br.ReadInt16();

                    if (maxmana > Main.player[ply].statManaMax + 20 || mana > maxmana)
                    {
                        if (players[ply].syncMP)
                        {
                            Tools.HandleCheater(ply);
                            Log.Info(Tools.FindPlayer(ply) +
                                     " had increased max mana by more than 20 or increased mana more than max");
                        }
                        else
                        {
                            players[ply].syncMP = true;
                        }
                    }
                }
            }
            else if (e.MsgID == 0x19) // Chat Text
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    byte ply = br.ReadByte();

                    if (e.Msg.whoAmI != ply)
                    {
                        //fuck you faggot
                        Log.Info(Tools.FindPlayer(e.Msg.whoAmI) + " was kicked for trying to fake chat as someone else.");
                        Tools.HandleCheater(ply);
                    }
                }
            }
            else if (e.MsgID == 0x1B) // New Projectile
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    short ident = br.ReadInt16();
                    float posx = br.ReadSingle();
                    float posy = br.ReadSingle();
                    float velx = br.ReadSingle();
                    float vely = br.ReadSingle();
                    float knockback = br.ReadSingle();
                    short dmg = br.ReadInt16();
                    byte owner = br.ReadByte();
                    byte type = br.ReadByte();

                    if (type == 29 || type == 28)
                    {
                        if (!players[e.Msg.whoAmI].group.HasPermission("ignoregriefdetection"))
                        {
                            if (ConfigurationManager.kickBoom || ConfigurationManager.banBoom)
                            {
                                int i = e.Msg.whoAmI;
                                if (ConfigurationManager.banBoom)
                                    FileTools.WriteGrief((int)i);
                                Tools.Kick((int)i, "Explosives were thrown.");
                                Tools.Broadcast(Main.player[i].name + " was " +
                                                (ConfigurationManager.banBoom ? "banned" : "kicked") +
                                                " for throwing an explosive device.");
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
            else if (e.MsgID == 0x2C) // KillMe
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    byte id = br.ReadByte();
                    byte hitdirection = br.ReadByte();
                    short dmg = br.ReadInt16();
                    bool pvp = br.ReadBoolean();

                    if (id != e.Msg.whoAmI)
                    {
                        Tools.HandleGriefer(e.Msg.whoAmI);
                        Log.Info(Tools.FindPlayer(e.Msg.whoAmI) +
                                 " was kicked for trying to execute KillMe on someone else.");
                        e.Handled = true;
                    }
                }
            }
            else if (e.MsgID == 0x30)
            {
                using (var br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    byte liquid = br.ReadByte();
                    bool lava = br.ReadBoolean();

                    if (ConfigurationManager.spawnProtect)
                    {
                        if (!players[e.Msg.whoAmI].group.HasPermission("editspawn"))
                        {
                            var flag = CheckSpawn(x, y);
                            if (flag)
                            {
                                Tools.SendMessage(e.Msg.whoAmI, "The spawn is protected!", new float[] { 255f, 0f, 0f });
                                e.Handled = true;
                            }
                        }
                    }
                    else if (e.MsgID == 0x22) // Client only KillTile
                    {
                        e.Handled = true; // Client only uses it for chests, but sends regular 17 as well.
                    }
                }
            }
        }

        void OnGreetPlayer(int who, HandledEventArgs e)
        {
            if (Main.netMode != 2) { return; }
            int plr = who; //legacy support
            Tools.ShowMOTD(who);
            if (Main.player[plr].statLifeMax > 400 || Main.player[plr].statManaMax > 200 || Main.player[plr].statLife > 400 || Main.player[plr].statMana > 200 || CheckInventory(plr))
            {
                Tools.HandleCheater(plr);
            }
            if (ConfigurationManager.permaPvp)
            {
                Main.player[who].hostile = true;
                NetMessage.SendData(30, -1, -1, "", who);
            }
            if (TShock.players[who].group.HasPermission("causeevents") && ConfigurationManager.infiniteInvasion && !ConfigurationManager.startedInvasion)
            {
                StartInvasion();
            }
            ShowUpdateReminder(who);
            e.Handled = true;
        }

        void OnChat(int ply, string msg, HandledEventArgs handler)
        {
            if (Main.netMode != 2) { return; }
            int x = (int)Main.player[ply].position.X;
            int y = (int)Main.player[ply].position.Y;

            if (msg.StartsWith("/"))
            {
                //Commands.CommandArgs args = new Commands.CommandArgs(msg, x, y, ply);
                Commands.Command cmd = null;
                for (int i = 0; i < Commands.commands.Count; i++)
                {
                    if (Commands.commands[i].Name().Equals(msg.Split(' ')[0].TrimStart('/')))
                    {
                        cmd = Commands.commands[i];
                    }
                }

                if (cmd == null)
                {
                    Tools.SendMessage(ply, "That command does not exist, try /help", new float[] { 255, 0, 0 });
                }
                else
                {
                    if (!cmd.Run(msg, players[ply]))
                    {
                        Log.Info(Tools.FindPlayer(ply) + " tried to execute " + cmd.Name() + " that s/he did not have access to!");
                        Tools.SendMessage(ply, "YOU DO NOT HAVE ACCESS TO THAT COMMAND YOU LITTLE SHIT!", new float[] { 255, 0, 0 });
                    }
                }
                handler.Handled = true;
            }
        }

        void OnJoin(int ply, AllowEventArgs handler)
        {
            if (Main.netMode != 2) { return; }
            string ip = Tools.GetRealIP((Convert.ToString(Netplay.serverSock[ply].tcpClient.Client.RemoteEndPoint)));
            if (FileTools.CheckBanned(ip))
            {
                Tools.Kick(ply, "You are banned.");
            }
            else if (Tools.FindPlayer(ply).Length > 32)
            {
                Tools.Kick(ply, "Your name was too long.");
                Tools.Broadcast(ip + " was kicked because their name exceeded 32 characters.");
            }
            else if (FileTools.CheckCheat(ip))
            {
                Tools.Kick(ply, "You were flagged for cheating.");
            }
            else if (FileTools.Checkgrief(ip))
            {
                Tools.Kick(ply, "You were flagged for griefing.");
            }
            if (!FileTools.OnWhitelist(ip))
            {
                Tools.Kick(ply, "Not on whitelist.");
            }
            players[ply] = new TSPlayer(ply);
            players[ply].group = Tools.GetGroupForIP(ip);
        }

        void OnLoadContent(Microsoft.Xna.Framework.Content.ContentManager obj)
        {
        }

        void OnPreInit()
        {
        }

        void OnPostInit()
        {
            if (!System.IO.File.Exists(FileTools.SaveDir + "auth.lck"))
            {
                Random r = new Random((int)System.DateTime.Now.ToBinary());
                ConfigurationManager.authToken = r.Next(100000, 10000000);
                Console.WriteLine("TShock Notice: To become SuperAdmin, join the game and type /auth " + ConfigurationManager.authToken);
                Console.WriteLine("This token will only display ONCE.");
                FileTools.CreateFile(FileTools.SaveDir + "auth.lck");
            }
        }

        void OnUpdate(GameTime time)
        {
            if (Main.netMode != 2) { return; }
            for (uint i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active == false) { continue; }
                if (players[i].tileThreshold >= 20)
                {
                    if (Main.player[i] != null)
                    {
                        if (ConfigurationManager.kickTnt || ConfigurationManager.banTnt)
                        {
                            if (ConfigurationManager.banTnt)
                                FileTools.WriteGrief((int)i);
                            Tools.Kick((int)i, "Kill tile abuse detected.");
                            Tools.Broadcast(Main.player[i].name + " was " + (ConfigurationManager.banTnt ? "banned" : "kicked") + " for kill tile abuse.");
                            RevertKillTile((int)i);
                        }
                        else if (players[i].tileThreshold > 0)
                        {
                            players[i].tileThreshold = 0;
                            players[i].tilesDestroyed.Clear();
                        }
                    }
                }
                else if (players[i].tileThreshold > 0)
                {
                    players[i].tileThreshold = 0;
                }
            }
        }

        /*
         * Useful stuff:
         * */

        public static void ShowUpdateReminder(int ply)
        {
            if (!shownVersion)
            {
                if (TShock.players[ply].group.HasPermission("maintenance"))
                {
                    WebClient client = new WebClient();
                    client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; .NET CLR 1.0.3705;)");
                    try
                    {
                        string updateString = client.DownloadString("http://shankshock.com/tshock-update.txt");
                        string[] changes = updateString.Split(',');
                        Version updateVersion = new Version(Convert.ToInt32(changes[0]), Convert.ToInt32(changes[1]), Convert.ToInt32(changes[2]), Convert.ToInt32(changes[3]));
                        float[] color = { 255, 255, 000 };
                        if (VersionNum.CompareTo(updateVersion) < 0)
                        {
                            Tools.SendMessage(ply, "This server is out of date.");
                            for (int i = 4; i <= changes.Length; i++)
                            {
                                Tools.SendMessage(ply, changes[i], color);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        FileTools.WriteError(e.Message);
                    }
                    shownVersion = true;
                }
            }
        }

        public static void Teleport(int ply, int x, int y)
        {
            /*Main.player[ply].velocity = new Vector2(0, 0);
            NetMessage.SendData(0x0d, -1, -1, "", ply);
            Main.player[ply].position.X = x;
            Main.player[ply].position.Y = y - 0x2a;
            NetMessage.SendData(0x0d, -1, -1, "", ply);
            UpdatePlayers();*/
            Main.player[ply].position.X = (float)x;
            Main.player[ply].position.Y = (float)y;
            NetMessage.SendData(0x0d, -1, ply, "", ply);
            NetMessage.SendData(0x0d, -1, -1, "", ply);
            NetMessage.syncPlayers();
        }

        public static void Teleport(int ply, float x, float y)
        {
            /*Main.player[ply].position.X = x;
            Main.player[ply].position.Y = y - 0x2a;
            NetMessage.SendData(0x14, -1, -1, "", 10, x, y);
            NetMessage.SendData(0x0d, -1, -1, "", ply);
            int oldx = Main.player[ply].SpawnX;
            int oldy = Main.player[ply].SpawnY;
            Main.player[ply].SpawnX = (int)(x / 16);
            Main.player[ply].SpawnY = (int)((y - 0x2a) / 16);
            NetMessage.SendData(0xC, -1, -1, "", ply);
            Main.player[ply].SpawnX = oldx;
            Main.player[ply].SpawnY = oldy;
            UpdatePlayers();*/
            Main.player[ply].position.X = x;
            Main.player[ply].position.Y = y;
            NetMessage.SendData(0x0d, -1, ply, "", ply);
            NetMessage.SendData(0x0d, -1, -1, "", ply);
            NetMessage.syncPlayers();
        }

        public static void StartInvasion()
        {
            Main.invasionType = 1;
            if (ConfigurationManager.infiniteInvasion)
            {
                Main.invasionSize = 20000000;
            }
            else
            {
                Main.invasionSize = 100 + (ConfigurationManager.invasionMultiplier * Tools.activePlayers());
            }

            Main.invasionWarn = 0;
            if (new Random().Next(2) == 0)
            {
                Main.invasionX = 0.0;
            }
            else
            {
                Main.invasionX = Main.maxTilesX;
            }
        }

        public static void IncrementKills()
        {
            ConfigurationManager.killCount++;
            Random r = new Random();
            int random = r.Next(5);
            if (ConfigurationManager.killCount % 100 == 0)
            {
                switch (random)
                {
                    case 0:
                        Tools.Broadcast("You call that a lot? " + ConfigurationManager.killCount + " goblins killed!");
                        break;
                    case 1:
                        Tools.Broadcast("Fatality! " + ConfigurationManager.killCount + " goblins killed!");
                        break;
                    case 2:
                        Tools.Broadcast("Number of 'noobs' killed to date: " + ConfigurationManager.killCount);
                        break;
                    case 3:
                        Tools.Broadcast("Duke Nukem would be proud. " + ConfigurationManager.killCount + " goblins killed.");
                        break;
                    case 4:
                        Tools.Broadcast("You call that a lot? " + ConfigurationManager.killCount + " goblins killed!");
                        break;
                    case 5:
                        Tools.Broadcast(ConfigurationManager.killCount + " copies of Call of Duty smashed.");
                        break;
                }
            }
        }

        public static void UpdateInventories()
        {
            for (int i = 0; i < Main.player.Length; i++)
            {
                for (int j = 0; j < 44; j++)
                {
                    for (int h = 0; h < Main.player.Length; h++)
                        NetMessage.SendData(5, h, i, Main.player[i].inventory[j].name, i, (float)j, 0f, 0f);
                }
            }
        }

        public static void UpdatePlayers()
        {
            for (int i = 0; i < Main.player.Length; i++)
            {
                for (int h = 0; h < Main.player.Length; h++)
                    NetMessage.SendData(0x0d, i, -1, "", h);
            }
        }

        public static void KillMe(int plr)
        {
            for (int i = 0; i < Main.player.Length; i++)
                NetMessage.SendData(44, i, -1, "", plr, (float)1, (float)9999999, (float)0);
        }

        public static void SendDataAll(int type, int ignore = -1, string text = "", int num = 0, float f1 = 0f, float f2 = 0f, float f3 = 0f)
        {
            for (int i = 0; i < Main.player.Length; i++)
                NetMessage.SendData(type, i, ignore, text, num, f1, f2, f3);
        }

        //TODO : Notify the player if there is more than one match. (or do we want a First() kinda thing?)
        public static int GetNPCID(string name, bool exact = false)
        {
            NPC npc = new NPC();
            for (int i = 1; i <= 45; i++)
            {
                if (exact)
                {
                    //Method #1 - must be exact match, allows support for different coloured slimes
                    npc.SetDefaults(name);
                    if (npc.name == name)
                        return i;
                }
                else
                {
                    //Method #2 - allows impartial matching
                    name = name.ToLower();
                    npc.SetDefaults(i);
                    if (npc.name.ToLower().StartsWith(name))
                        return i;
                }
            }
            return -1;
        }

        public static int GetItemID(string name)
        {
            Item item = new Item();
            name = name.ToLower();
            for (int i = 1; i <= 238; i++)
            {
                item.SetDefaults(i);
                if (item.name.ToLower().StartsWith(name))
                    return i;
            }
            return -1;
        }

        public static bool CheckInventory(int plr)
        {
            for (int i = 0; i < 44; i++)
            {
                if (Main.player[plr].inventory[i].stack > Main.player[plr].inventory[i].maxStack)
                {
                    Log.Info(Tools.FindPlayer(plr) + " had " + Main.player[plr].inventory[i].stack.ToString() + " of " + Main.player[plr].inventory[i].name + " which has a max stack of " + Main.player[plr].inventory[i].maxStack.ToString());
                    return true;
                }
            }
            return false;
        }

        public static bool CheckSpawn(int x, int y)
        {
            Vector2 tile = new Vector2((float)x, (float)y);
            Vector2 spawn = new Vector2((float)Main.spawnTileX, (float)Main.spawnTileY);
            var distance = Vector2.Distance(spawn, tile);
            if (distance > (float)ConfigurationManager.spawnProtectRadius)
                return false;
            else
                return true;
        }

        public class Position
        {
            public float X;
            public float Y;

            public Position(float x, float y) { X = x; Y = y; }
        }

        public static void RevertKillTile(int ply)
        {
            Tile[] tiles = new Tile[players[ply].tilesDestroyed.Count];
            players[ply].tilesDestroyed.Values.CopyTo(tiles, 0);
            Position[] positions = new Position[players[ply].tilesDestroyed.Count];
            players[ply].tilesDestroyed.Keys.CopyTo(positions, 0);
            for (int i = (players[ply].tilesDestroyed.Count - 1); i >= 0; i--)
            {
                Main.tile[(int)positions[i].X, (int)positions[i].Y] = tiles[i];
                NetMessage.SendData(17, -1, -1, "", 1, positions[i].X, positions[i].Y, (float)0);
            }
        }
    }
}