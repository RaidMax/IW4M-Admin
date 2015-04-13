using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Collections;

namespace IW4MAdmin
{
    abstract class Database
    {
        public Database(String FN)
        {
            FileName = FN;
            DBCon = String.Format("Data Source={0}", FN);
            Con = new SQLiteConnection(DBCon);
            Open = false;
            Init();
        }

        abstract public void Init();
        
        //HELPERS
        protected bool Insert(String tableName, Dictionary<String, object> data)
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

        protected bool Update(String tableName, Dictionary<String, object> data, String where)
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
                ExecuteNonQuery(String.Format("update {0} set {1} where {2};", tableName, vals, where));
            }
            catch (Exception fail)
            {
                Console.WriteLine(fail.Message);
                returnCode = false;
            }
            return returnCode;
        }

        protected DataRow getDataRow(String Q)
        {
            DataRow Result = GetDataTable(Q).Rows[0];
            return Result;
        }

        protected int ExecuteNonQuery(String Request)
        {
            waitForClose();
            int rowsUpdated = 0;

            lock (Con)
            {
                Con.Open();
                SQLiteCommand CMD = new SQLiteCommand(Con);
                CMD.CommandText = Request;
                rowsUpdated = CMD.ExecuteNonQuery();
                Con.Close();
            }

            return rowsUpdated;
        }

        protected DataTable GetDataTable(String sql)
        {
            DataTable dt = new DataTable();
            try
            {
                waitForClose();
                lock (Con)
                {
                    Con.Open();
                    SQLiteCommand mycommand = new SQLiteCommand(Con);
                    mycommand.CommandText = sql;
                    SQLiteDataReader reader = mycommand.ExecuteReader();
                    dt.Load(reader);
                    reader.Close();
                    Con.Close();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new Exception(e.Message);
            }
            return dt;
        }

        protected void waitForClose()
        {
            while (Con.State == ConnectionState.Open)
            {
                Utilities.Wait(0.01);
            }

            return;
        }
        //END

        protected String FileName;
        protected String DBCon;
        protected SQLiteConnection Con;
        protected bool Open;
    }

    class ClientsDB : Database
    {
        public ClientsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if(!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [CLIENTS] ( [Name] TEXT  NULL, [npID] TEXT  NULL, [Number] INTEGER PRIMARY KEY AUTOINCREMENT, [Level] INT DEFAULT 0 NULL, [LastOffense] TEXT NULL, [Connections] INT DEFAULT 1 NULL, [IP] TEXT NULL, [LastConnection] TEXT NULL);";
                ExecuteNonQuery(Create);
                Create = "CREATE TABLE [BANS] ( [Reason] TEXT NULL, [npID] TEXT NULL, [bannedByID] TEXT NULL, [IP] TEXT NULL, [TIME] TEXT NULL);";
                ExecuteNonQuery(Create);
            }
        }

        //Returns a single player object with matching GUID, false if no matches
        public Player getPlayer(String ID, int cNum)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE npID = '{0}' LIMIT 1", ID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                DateTime LC;
                
                try
                {
                    LC = DateTime.Parse(ResponseRow["LastConnection"].ToString());
                }
                catch (Exception)
                {
                    LC = DateTime.Now;
                }
 
                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), cNum, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), LC);
            }

            else
                return null;
        }

        //Overloaded method for getPlayer, returns Client with matching DBIndex, null if none found
        public Player getPlayer(int dbIndex)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Number = '{0}' LIMIT 1", dbIndex);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            { 
                DataRow p = Result.Rows[0];
                DateTime LC;
                try
                {
                    LC = DateTime.Parse(p["LastConnection"].ToString());
                }
                catch (Exception)
                {
                    LC = DateTime.Now;
                }

                return new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32(p["Connections"]), p["IP"].ToString(), LC);
            }

            else
                return null;
        }

        //Returns a list of players matching name parameter, null if no players found matching
        public List<Player> findPlayers(String name)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Name LIKE '%{0}%' LIMIT 8", name);
            DataTable Result = GetDataTable(Query);

            List<Player> Players = new List<Player>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                {
                    DateTime LC;
                    try
                    {
                        LC = DateTime.Parse(p["LastConnection"].ToString());
                    }
                    catch (Exception)
                    {
                        LC = DateTime.Now;
                    }

                    Players.Add(new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32(p["Connections"]), p["IP"].ToString(), LC));
                }
                return Players;
            }

            else
                return null;
        }

        //Returns any player with level 4 permissions, null if no owner found
        public Player getOwner()
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Level >= '{0}'", 4);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                if (ResponseRow["IP"].ToString().Length < 6)
                    ResponseRow["IP"] = "0";
                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), null, 0, ResponseRow["IP"].ToString());
            }

            else
                return null;
        }

        //Returns list of bans in database
        public List<Ban> getBans()
        {
            List<Ban> Bans = new List<Ban>();
            DataTable Result = GetDataTable("SELECT * FROM BANS ORDER BY TIME DESC");

            foreach (DataRow Row in Result.Rows)
            {
                if (Row["TIME"].ToString().Length < 2) //compatibility with my old database
                    Row["TIME"] = DateTime.Now.ToString();

                Bans.Add(new Ban(Row["Reason"].ToString(), Row["npID"].ToString(), Row["bannedByID"].ToString(), DateTime.Parse(Row["TIME"].ToString()), Row["IP"].ToString()));
            }

            return Bans;
        }

        //Returns total number of player entries in database
        public int totalPlayers()
        {
            DataTable Result = GetDataTable("SELECT * from CLIENTS ORDER BY Number DESC LIMIT 1");
            if (Result.Rows.Count > 0)
                return Convert.ToInt32(Result.Rows[0]["Number"]);
            else
                return 0;
        }

        //Add specified player to database
        public void addPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            newPlayer.Add("Name", Utilities.removeNastyChars(P.getName()));
            newPlayer.Add("npID", P.getID());
            newPlayer.Add("Level", (int)P.getLevel());
            newPlayer.Add("LastOffense", "");
            newPlayer.Add("Connections", 1);
            newPlayer.Add("IP", P.getIP());
            newPlayer.Add("LastConnection", Utilities.DateTimeSQLite(DateTime.Now));

            Insert("CLIENTS", newPlayer);
        }

        ///Update information of specified player
        public void updatePlayer(Player P)
        {
            Dictionary<String, Object> updatedPlayer = new Dictionary<String, Object>();

            updatedPlayer.Add("Name", P.getName());
            updatedPlayer.Add("npID", P.getID());
            updatedPlayer.Add("Level", (int)P.getLevel());
            updatedPlayer.Add("LastOffense", P.getLastO());
            updatedPlayer.Add("Connections", P.getConnections());
            updatedPlayer.Add("IP", P.getIP());
            updatedPlayer.Add("LastConnection", Utilities.DateTimeSQLite(DateTime.Now));

            Update("CLIENTS", updatedPlayer, String.Format("npID = '{0}'", P.getID()));
        }


        //Add specified ban to database
        public void addBan(Ban B)
        {
            Dictionary<String, object> newBan = new Dictionary<String, object>();

            newBan.Add("Reason", B.getReason());
            newBan.Add("npID", B.getID());
            newBan.Add("bannedByID", B.getBanner());
            newBan.Add("IP", B.getIP());
            newBan.Add("TIME", Utilities.DateTimeSQLite(DateTime.Now));

            Insert("BANS", newBan);
        }


        //Deletes ban with matching GUID
        public void removeBan(String GUID)
        {
            String Query = String.Format("DELETE FROM BANS WHERE npID = '{0}'", GUID);
            ExecuteNonQuery(Query);
        }

        public void removeBan(String GUID, String IP)
        {
            String Query = String.Format("DELETE FROM BANS WHERE npID = '{0}' or IP= '%{1}%'", GUID, IP);
            ExecuteNonQuery(Query);
        }
        

    }

    class StatsDB : Database
    {
        public StatsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [STATS] ( [Number] INTEGER, [KILLS] INTEGER DEFAULT 0, [DEATHS] INTEGER DEFAULT 0, [KDR] REAL DEFAULT 0, [SKILL] REAL DEFAULT 0, [MEAN] REAL DEFAULT 0, [DEV] REAL DEFAULT 0 );"; 
                ExecuteNonQuery(Create);
            }
        }

        // Return stats for player specified by Database ID, null if no matches
        public Stats getStats(int DBID)
        {
            String Query = String.Format("SELECT * FROM STATS WHERE Number = '{0}'", DBID);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                if (ResponseRow["MEAN"] == DBNull.Value)
                    ResponseRow["MEAN"] = Moserware.Skills.GameInfo.DefaultGameInfo.DefaultRating.Mean;
                if (ResponseRow["DEV"] == DBNull.Value)
                    ResponseRow["DEV"] = Moserware.Skills.GameInfo.DefaultGameInfo.DefaultRating.StandardDeviation;
                if (ResponseRow["SKILL"] == DBNull.Value)
                    ResponseRow["SKILL"] = 0;

                return new Stats(Convert.ToInt32(ResponseRow["Number"]), Convert.ToInt32(ResponseRow["KILLS"]), Convert.ToInt32(ResponseRow["DEATHS"]), Convert.ToDouble(ResponseRow["KDR"]), Convert.ToDouble(ResponseRow["SKILL"]), Convert.ToDouble(ResponseRow["MEAN"]), Convert.ToDouble(ResponseRow["DEV"]));
            }

            else
                return null;
        }

        public void addPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            newPlayer.Add("Number", P.getDBID());
            newPlayer.Add("KILLS", 0);
            newPlayer.Add("DEATHS", 0);
            newPlayer.Add("KDR", 0);
            newPlayer.Add("SKILL", Moserware.Skills.GameInfo.DefaultGameInfo.DefaultRating.ConservativeRating);
            newPlayer.Add("MEAN", Moserware.Skills.GameInfo.DefaultGameInfo.DefaultRating.Mean);
            newPlayer.Add("DEV", Moserware.Skills.GameInfo.DefaultGameInfo.DefaultRating.StandardDeviation);

            Insert("STATS", newPlayer);
        }

        //Update stat information of specified player
        public void updatePlayer(Player P)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            updatedPlayer.Add("KILLS", P.stats.Kills);
            updatedPlayer.Add("DEATHS", P.stats.Deaths);
            updatedPlayer.Add("KDR", Math.Round(P.stats.KDR, 2));
            updatedPlayer.Add("SKILL", P.stats.Skill);
            updatedPlayer.Add("MEAN", P.stats.Rating.Mean);
            updatedPlayer.Add("DEV", P.stats.Rating.StandardDeviation);

            Update("STATS", updatedPlayer, String.Format("Number = '{0}'", P.getDBID()));
        }

        //Returns top 8 players (we filter through them later)
        public List<Stats> topStats()
        {
            String Query = String.Format("SELECT * FROM STATS WHERE SKILL > '{0}' ORDER BY SKILL DESC LIMIT 5", 230);
            DataTable Result = GetDataTable(Query);

            List<Stats> Top = new List<Stats>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow D in Result.Rows)
                {
                    if (D["MEAN"] == DBNull.Value)
                        continue;
                    if (D["DEV"] == DBNull.Value)
                        continue;

                    if (D["SKILL"] == DBNull.Value)
                        D["SKILL"] = 0;

                    Stats S = new Stats(Convert.ToInt32(D["Number"]), Convert.ToInt32(D["KILLS"]), Convert.ToInt32(D["DEATHS"]), Convert.ToDouble(D["KDR"]), Convert.ToDouble(D["SKILL"]), Convert.ToDouble(D["MEAN"]), Convert.ToDouble(D["DEV"]));
                    if (S.Skill > 230)
                        Top.Add(S);
                }
            }

            return Top;
        }

        public List<Stats> getMultipleStats(int start, int length)
        {
            String Query = String.Format("SELECT * FROM STATS ORDER BY SKILL DESC LIMIT '{0}' OFFSET '{1}'", length, start);
            DataTable Result = GetDataTable(Query);

            List<Stats> Stats = new List<Stats>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow D in Result.Rows)
                {
                    if (D["MEAN"] == DBNull.Value)
                        continue;
                    if (D["DEV"] == DBNull.Value)
                        continue;

                    if (D["SKILL"] == DBNull.Value)
                        D["SKILL"] = 0;

                    Stats S = new Stats(Convert.ToInt32(D["Number"]), Convert.ToInt32(D["KILLS"]), Convert.ToInt32(D["DEATHS"]), Convert.ToDouble(D["KDR"]), Convert.ToDouble(D["SKILL"]), Convert.ToDouble(D["MEAN"]), Convert.ToDouble(D["DEV"]));
                    Stats.Add(S);
                }
            }

            return Stats;
        }

        public int totalStats()
        {
            DataTable Result = GetDataTable("SELECT * FROM STATS");
            return Result.Rows.Count;
        }

        public void clearSkill()
        {
            String Query = "SELECT * FROM STATS";
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow D in Result.Rows)
                    Update("STATS", new Dictionary<String, Object> () { {"SKILL",  1} }, String.Format("Number = '{0}'", D["Number"]));
            }
        }
    }

    class AliasesDB : Database
    {
        public AliasesDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [ALIASES] ( [Number] INTEGER, [NAMES] TEXT NULL, [IPS] TEXTNULL );";
                ExecuteNonQuery(Create);
            }
        }

        public Aliases getPlayer(int dbIndex)
        {
            String Query = String.Format("SELECT * FROM ALIASES WHERE Number = '{0}' LIMIT 1", dbIndex);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow p = Result.Rows[0];
                return new Aliases(Convert.ToInt32(p["Number"]), p["NAMES"].ToString(), p["IPS"].ToString());
            }

            else
                return null;
        }

        public List<Aliases> getPlayer(String IP)
        {
            String Query = String.Format("SELECT * FROM ALIASES WHERE IPS LIKE '%{0}%'", IP);
            DataTable Result = GetDataTable(Query);
            List<Aliases> players = new List<Aliases>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                 players.Add(new Aliases(Convert.ToInt32(p["Number"]), p["NAMES"].ToString(), p["IPS"].ToString()));
            }

            return players;
        }

        public List<Aliases> findPlayers(String name)
        {
            String Query = String.Format("SELECT * FROM ALIASES WHERE NAMES LIKE '%{0}%' LIMIT 8", name);
            DataTable Result = GetDataTable(Query);

            List<Aliases> players = new List<Aliases>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                    players.Add(new Aliases(Convert.ToInt32(p["Number"]), p["NAMES"].ToString(), p["IPS"].ToString()));
            }

            return players;
        }

        public void addPlayer(Aliases Alias)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>();

            newPlayer.Add("Number", Alias.getNumber());
            newPlayer.Add("NAMES", Alias.getNamesDB());
            newPlayer.Add("IPS", Alias.getIPSDB());

            Insert("ALIASES", newPlayer);
        }

        public void updatePlayer(Aliases Alias)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            updatedPlayer.Add("Number", Alias.getNumber());
            updatedPlayer.Add("NAMES", Alias.getNamesDB());
            updatedPlayer.Add("IPS", Alias.getIPSDB());

            Update("ALIASES", updatedPlayer, String.Format("Number = '{0}'", Alias.getNumber()));
        }
    }
}
