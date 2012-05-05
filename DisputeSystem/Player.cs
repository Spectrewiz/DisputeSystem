using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace DisputeSystem
{
    public class Player
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }

        public Player(int index)
        {
            Index = index;
        }

        public static Player GetPlayerByName(string name)
        {
            var player = TShock.Utils.FindPlayer(name)[0];
            if (player != null)
            {
                foreach (Player ply in DisputeSystem.Players)
                {
                    if (ply.TSPlayer == player)
                    {
                        return ply;
                    }
                }
            }
            return null;
        }

        protected Banned ban = Banned.free;
        public void Ban(Banned ban)
        {
            this.ban = ban;
        }
        public Banned GetBan()
        {
            return ban;
        }
        public enum Banned
        {
            free,
            banned
        }
    }
}
