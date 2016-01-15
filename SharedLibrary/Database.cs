using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using System.Data;
using System.IO;

namespace SharedLibrary
{
    public abstract class Database
    {
        public Database(String FN)
        {
            FileName = FN;
            DBCon = String.Format("Data Source={0}", FN);
            try
            {
                Con = new SQLiteConnection(DBCon);
            }

            catch(System.DllNotFoundException)
            {
                Console.WriteLine("Fatal Error: could not locate the SQLite DLL(s)!\nEnsure they are located in the 'lib' folder");
                Utilities.Wait(5);
                System.Environment.Exit(0);
            }
            
            Open = false;
            Init();
        }

        abstract public void Init();

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
                return new DataTable();
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

        protected String FileName;
        protected String DBCon;
        protected SQLiteConnection Con;
        protected bool Open;
    }

    public class ClientsDB : Database
    {
        public ClientsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [CLIENTS] ( [Name] TEXT  NULL, [npID] TEXT  NULL, [Number] INTEGER PRIMARY KEY AUTOINCREMENT, [Level] INT DEFAULT 0 NULL, [LastOffense] TEXT NULL, [Connections] INT DEFAULT 1 NULL, [IP] TEXT NULL, [LastConnection] TEXT NULL);";
                ExecuteNonQuery(Create);
                Create = "CREATE TABLE [BANS] ( [TYPE] TEXT NULL, [Reason] TEXT NULL, [npID] TEXT NULL, [bannedByID] TEXT NULL, [IP] TEXT NULL, [TIME] TEXT NULL);";
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
                DateTime lastCon = DateTime.MinValue;
                DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), cNum, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon);
            }

            else
                return null;
        }

        public List<Player> getPlayers(List<String> npIDs)
        {
            List<Player> returnssss = new List<Player>();
            String test = String.Join("' OR npID = '", npIDs);
           
            String Query = String.Format("SELECT * FROM CLIENTS WHERE npID = '{0}'", test);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    DateTime lastCon = DateTime.MinValue;
                    DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                    returnssss.Add(new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon));
                }
            }

            return returnssss;
        }

        public List<Player> getPlayers(List<int> databaseIDs)
        {
            List<Player> returnssss = new List<Player>();
            String test = String.Join("' OR Number = '", databaseIDs);

            String Query = String.Format("SELECT * FROM CLIENTS WHERE Number = '{0}'", test);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    DateTime lastCon = DateTime.MinValue;
                    DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                    returnssss.Add(new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon));
                }
            }

            return returnssss;
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
                    LC = DateTime.MinValue;
                }

                return new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32(p["Connections"]), p["IP"].ToString(), LC);
            }

            else
                return null;
        }

        //get player by ip, (used for webfront)
        public Player getPlayer(String IP)
        {
            String Query = String.Format("SELECT * FROM CLIENTS WHERE IP = '{0}'", IP);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                List<Player> lastKnown = new List<Player>();
                foreach (DataRow p in Result.Rows)
                {
                    DateTime LC;
                    try
                    {
                        LC = DateTime.Parse(p["LastConnection"].ToString());
                        lastKnown.Add(new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32((DateTime.Now - LC).TotalSeconds), p["IP"].ToString(), LC));
                    }

                    catch (Exception)
                    {
                        continue;
                    }
                }

                if (lastKnown.Count > 0)
                {
                    List<Player> Returning = lastKnown.OrderBy(t => t.Connections).ToList();
                    return Returning[0];
                }

                else
                    return null;
            }

            else
                return null;
        }

        //Returns a list of players matching name parameter, null if no players found matching
        public List<Player> findPlayers(String name)
        {
            name = name.Replace("'", "");
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Name LIKE '%{0}%' LIMIT 32", name);
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
                        LC = DateTime.MinValue;
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
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Level > '{0}'", 4);
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
        public List<Penalty> getBans()
        {
            List<Penalty> Bans = new List<Penalty>();
            DataTable Result = GetDataTable("SELECT * FROM BANS ORDER BY TIME DESC");

            foreach (DataRow Row in Result.Rows)
            {
                if (Row["TIME"].ToString().Length < 2) //compatibility with my old database
                    Row["TIME"] = DateTime.Now.ToString();

                SharedLibrary.Penalty.Type BanType = Penalty.Type.Ban;
                if (Row["TYPE"].ToString().Length != 0)
                    BanType = (Penalty.Type)Enum.Parse(typeof(Penalty.Type), Row["TYPE"].ToString());

                Bans.Add(new Penalty(BanType, Row["Reason"].ToString(), Row["npID"].ToString(), Row["bannedByID"].ToString(), DateTime.Parse(Row["TIME"].ToString()), Row["IP"].ToString()));
          
            }

            return Bans;
        }

        //Returns all players with level > Flagged
        public List<Player> getAdmins()
        {
            List<Player> Admins = new List<Player>();
            String Query = String.Format("SELECT * FROM CLIENTS WHERE LEVEL > '{0}'", 1);
            DataTable Result = GetDataTable(Query);

            foreach (DataRow P in Result.Rows)
                Admins.Add(new Player(P["Name"].ToString(), P["npID"].ToString(), (Player.Permission)P["Level"], P["IP"].ToString()));

            return Admins;
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

            newPlayer.Add("Name", Utilities.removeNastyChars(P.Name));
            newPlayer.Add("npID", P.npID);
            newPlayer.Add("Level", (int)P.Level);
            newPlayer.Add("LastOffense", "");
            newPlayer.Add("Connections", 1);
            newPlayer.Add("IP", P.IP);
            newPlayer.Add("LastConnection", Utilities.DateTimeSQLite(DateTime.Now));

            Insert("CLIENTS", newPlayer);
        }

        ///Update information of specified player
        public void updatePlayer(Player P)
        {
            Dictionary<String, Object> updatedPlayer = new Dictionary<String, Object>();

            updatedPlayer.Add("Name", P.Name);
            updatedPlayer.Add("npID", P.npID);
            updatedPlayer.Add("Level", (int)P.Level);
            updatedPlayer.Add("LastOffense", P.lastOffense);
            updatedPlayer.Add("Connections", P.Connections);
            updatedPlayer.Add("IP", P.IP);
            updatedPlayer.Add("LastConnection", Utilities.DateTimeSQLite(DateTime.Now));

            Update("CLIENTS", updatedPlayer, String.Format("npID = '{0}'", P.npID));
        }


        //Add specified ban to database
        public void addBan(Penalty B)
        {
            Dictionary<String, object> newBan = new Dictionary<String, object>();

            newBan.Add("Reason", B.Reason);
            newBan.Add("npID", B.npID);
            newBan.Add("bannedByID", B.bannedByID);
            newBan.Add("IP", B.IP);
            newBan.Add("TIME", Utilities.DateTimeSQLite(DateTime.Now));
            newBan.Add("TYPE", B.BType);

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

    public class AliasesDB : Database
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
            name = name.Replace("'", "");
            String[] EyePee = name.Split('.');
            String Penor = "THISISNOTANIP";
            if (EyePee.Length > 1)
                Penor = (EyePee[0] + '.' + EyePee[1] + '.');

            String Query = String.Format("SELECT * FROM ALIASES WHERE NAMES LIKE '%{0}%' OR IPS LIKE '%{1}%' LIMIT 15", name, Penor);
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

            newPlayer.Add("Number", Alias.Number);
            newPlayer.Add("NAMES", String.Join(";", Alias.Names));
            newPlayer.Add("IPS", String.Join(";", Alias.IPS));

            Insert("ALIASES", newPlayer);
        }

        public void updatePlayer(Aliases Alias)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>();

            updatedPlayer.Add("Number", Alias.Number);
            updatedPlayer.Add("NAMES", String.Join(";", Alias.Names));
            updatedPlayer.Add("IPS", String.Join(";", Alias.IPS));

            Update("ALIASES", updatedPlayer, String.Format("Number = '{0}'", Alias.Number));
        }
    }
}
