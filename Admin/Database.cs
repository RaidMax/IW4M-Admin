using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Collections;

namespace IW4MAdmin
{
    class Database
    {
        public enum Type
        {
            Clients,
            Stats
        }

        public Database(String FN, Type T)
        {
            FileName = FN;
            DBCon = String.Format("Data Source={0}", FN);
            Con = new SQLiteConnection(DBCon);
            DBType = T;
            Init(T);
        }

        private void Init(Type T)
        {
            if(!File.Exists(FileName))
            {
                switch (T)
                {
                    case Type.Clients:
                        String query = "CREATE TABLE [CLIENTS] ( [Name] TEXT  NULL, [npID] TEXT  NULL, [Number] INTEGER PRIMARY KEY AUTOINCREMENT, [Level] INT DEFAULT 0 NULL, [LastOffense] TEXT NULL, [Connections] INT DEFAULT 1 NULL);";
                        ExecuteNonQuery(query);
                        query = "CREATE TABLE [BANS] ( [Reason] TEXT NULL, [npID] TEXT NULL, [bannedByID] Text NULL);";
                        ExecuteNonQuery(query);
                        break;
                    case Type.Stats:
                        String query_stats = "CREATE TABLE [STATS] ( [Number] INTEGER, [KILLS] INTEGER DEFAULT 0, [DEATHS] INTEGER DEFAULT 0, [KDR] REAL DEFAULT 0, [SKILL] REAL DEFAULT 0 );";
                        ExecuteNonQuery(query_stats);
                        break;
                }
                
            }
        }

        public Player getPlayer(String ID, int cNum)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE npID = '{0}' LIMIT 1", ID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), cNum, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"]);
            }

            else
                return null;
        }

        public List<Player> findPlayers(String name)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Name LIKE '%{0}%' LIMIT 10", name);
            DataTable Result = GetDataTable(Query);

            List<Player> Players = new List<Player>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                {
                    Players.Add(new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), ((int)p["Connections"])));
                }
                return Players;
            }

            else
                return null;
        }

        public Player findPlayers(int dbIndex)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Number = '{0}' LIMIT 1", dbIndex);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            { 
               foreach (DataRow p in Result.Rows)
                    return new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), ((int)p["Connections"]));
            }
     
            return null;
        }

        public Player getOwner()
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Level = '{0}'", 4);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), null, 0);
            }

            else
                return null;
        }

        public List<Ban> getBans()
        {
            List<Ban> Bans = new List<Ban>();
            DataTable Result = GetDataTable("SELECT * FROM BANS");

            foreach (DataRow Row in Result.Rows)
                Bans.Add(new Ban(Row["Reason"].ToString(), Row["npID"].ToString(), Row["bannedByID"].ToString()));

            return Bans;
        }

        public Stats getStats(int DBID)
        {
            String Query = String.Format("SELECT * FROM STATS WHERE Number = '{0}'", DBID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                return new Stats(Convert.ToInt32(ResponseRow["KILLS"]), Convert.ToInt32(ResponseRow["DEATHS"]), Convert.ToDouble(ResponseRow["KDR"]), Convert.ToDouble(ResponseRow["SKILL"]));
            }

            else
                return null;
        }

        public void removeBan(String GUID)
        {
            String Query = String.Format("DELETE FROM BANS WHERE npID = '{0}'", GUID);
            ExecuteNonQuery(Query);
        }

        public void addPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            if (DBType == Type.Clients)
            {
                newPlayer.Add("Name", Utilities.removeNastyChars(P.getName()));
                newPlayer.Add("npID", P.getID());
                newPlayer.Add("Level", (int)P.getLevel());
                newPlayer.Add("LastOffense", "");
                newPlayer.Add("Connections", 1);

                Insert("CLIENTS", newPlayer);
            }
            
            if (DBType == Type.Stats)
            {
                newPlayer.Add("Number", P.getDBID());
                newPlayer.Add("KILLS", 0);
                newPlayer.Add("DEATHS", 0);
                newPlayer.Add("KDR", 0);
                newPlayer.Add("SKILL", 0);
                Insert("STATS", newPlayer);
            }
        }

        public List<Stats> topStats()
        {
            String Query = String.Format("SELECT * FROM STATS WHERE SKILL > '{0}' LIMIT 4", 20);
            DataTable Result = GetDataTable(Query);

            List<Stats> Top = new List<Stats>();
            
            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow D in Result.Rows)
                {
                    Stats S = new Stats(Convert.ToInt32(D["Number"]), Convert.ToInt32(D["DEATHS"]), Convert.ToDouble(D["KDR"]), Convert.ToDouble(D["SKILL"]));
                    Top.Add(S);
                }
            }

            return Top;     
        }

        public void updatePlayer(Player P)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            if (DBType == Type.Clients)
            {
                updatedPlayer.Add("Name", P.getName());
                updatedPlayer.Add("npID", P.getID());
                updatedPlayer.Add("Level", (int)P.getLevel());
                updatedPlayer.Add("LastOffense", P.getLastO());
                updatedPlayer.Add("Connections", P.getConnections());

                Update("CLIENTS", updatedPlayer, String.Format("npID = '{0}'", P.getID()));
            }

            if (DBType == Type.Stats)
            {
                updatedPlayer.Add("KILLS", P.stats.Kills);
                updatedPlayer.Add("DEATHS", P.stats.Deaths);
                updatedPlayer.Add("KDR", Math.Round(P.stats.KDR, 2));
                updatedPlayer.Add("SKILL", P.stats.Skill);

                Update("STATS", updatedPlayer, String.Format("Number = '{0}'", P.getDBID()));
            }
        }

        public void addBan(Ban B)
        {
            Dictionary<String, object> newBan = new Dictionary<String, object>();

            newBan.Add("Reason", B.getReason());
            newBan.Add("npID", B.getID());
            newBan.Add("bannedByID", B.getBanner());
   
            Insert("BANS", newBan);
        }

        //HELPERS

        public bool Insert(String tableName, Dictionary<String, object> data)
        {
            String columns = "";
            String values = "";
            Boolean returnCode = true;
            foreach (KeyValuePair<String, object> val in data)
            {
                columns += String.Format(" {0},", val.Key);
                values += String.Format(" '{0}',", val.Value);
            }
            columns = columns.Substring(0, columns.Length - 1);
            values = values.Substring(0, values.Length - 1);
            try
            {
                this.ExecuteNonQuery(String.Format("insert into {0}({1}) values({2});", tableName, columns, values));
            }
            catch (Exception fail)
            {
                Console.WriteLine(fail.Message);
                returnCode = false;
            }
            return returnCode;
        }

        public bool Update(String tableName, Dictionary<String, object> data, String where)
        {
            String vals = "";
            Boolean returnCode = true;
            if (data.Count >= 1)
            {
                foreach (KeyValuePair<String, object> val in data)
                {
                    vals += String.Format(" {0} = '{1}',", val.Key, val.Value);
                }
                vals = vals.Substring(0, vals.Length - 1);
            }
            try
            {
                this.ExecuteNonQuery(String.Format("update {0} set {1} where {2};", tableName, vals, where));
            }
            catch (Exception fail)
            {
                Console.WriteLine(fail.Message);
                returnCode = false;
            }
            return returnCode;
        }

        public DataRow getDataRow(String Q)
        {
            DataRow Result = GetDataTable(Q).Rows[0];
            return Result;
        }

        private int ExecuteNonQuery(String Request)
        {
            Con.Open();
            SQLiteCommand CMD = new SQLiteCommand(Con);
            CMD.CommandText = Request;
            int rowsUpdated = CMD.ExecuteNonQuery();
            Con.Close();
            return rowsUpdated;
        }

        public DataTable GetDataTable(String sql)
        {
            DataTable dt = new DataTable();
            try
            {
                Con.Open();
                SQLiteCommand mycommand = new SQLiteCommand(Con);
                mycommand.CommandText = sql;
                SQLiteDataReader reader = mycommand.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                Con.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception(e.Message);
            }
            return dt;
        }
        //END

        private String FileName;
        private String DBCon;
        private SQLiteConnection Con;
        private Type DBType;
    }
}
