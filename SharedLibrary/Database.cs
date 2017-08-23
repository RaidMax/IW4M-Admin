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
            Init();
        }

        protected SQLiteConnection GetNewConnection()
        {
            return new SQLiteConnection($"Data Source={FileName}");
        }

        abstract public void Init();

        protected bool Insert(String tableName, Dictionary<String, object> data)
        {
            string names = "";
            string parameters = "";
            foreach (string key in data.Keys)
            {
                names += key + ',';
                parameters += '@' + key + ',';
            }
            names = names.Substring(0, names.Length - 1);
            parameters = parameters.Substring(0, parameters.Length - 1);

            var Con = GetNewConnection();

            SQLiteCommand insertcmd = new SQLiteCommand()
            {
                Connection = Con,
                CommandText = String.Format("INSERT INTO `{0}` ({1}) VALUES ({2});", tableName, names, parameters)
            };
            foreach (string key in data.Keys)
            {
                insertcmd.Parameters.AddWithValue('@' + key, data[key]);
            }

            try
            {
                Con.Open();
                insertcmd.ExecuteNonQuery();
                Con.Close();
                return true;
            }

            catch (Exception E)
            {
                Console.WriteLine($"Line 58: {E.Message}");
                return false;
            }

        }

        protected bool Update(String tableName, Dictionary<String, object> data, KeyValuePair<string, object> where)
        {
            string parameters = "";
            foreach (string key in data.Keys)
            {
                parameters += key + '=' + '@' + key + ',';
            }

            parameters = parameters.Substring(0, parameters.Length - 1);
            var Con = GetNewConnection();

            SQLiteCommand updatecmd = new SQLiteCommand()
            {
                Connection = Con,
                CommandText = String.Format("UPDATE `{0}` SET {1} WHERE {2}=@{2}", tableName, parameters, where.Key)
            };
            foreach (string key in data.Keys)
            {
                updatecmd.Parameters.AddWithValue('@' + key, data[key]);
            }

            updatecmd.Parameters.AddWithValue('@' + where.Key, where.Value);

            try
            {
                Con.Open();
                updatecmd.ExecuteNonQuery();
                Con.Close();
                return true;
            }

            catch (Exception E)
            {
                Console.WriteLine($"Line 96: {E.Message}");
                return false;
            }
        }

        protected DataRow GetDataRow(String Q)
        {
            DataRow Result = GetDataTable(Q).Rows[0];
            return Result;
        }

        protected int ExecuteNonQuery(String Request)
        {
            int rowsUpdated = 0;
            Request = Request.Replace("!'", "").Replace("!", "");
            var Con = GetNewConnection();
            try
            {

                    Con.Open();
                SQLiteCommand CMD = new SQLiteCommand(Con)
                {
                    CommandText = Request
                };
                rowsUpdated = CMD.ExecuteNonQuery();
                    Con.Close();
                return rowsUpdated;
            }

            catch (Exception E)
            {
                // fixme: this needs to have a reference to a logger..
                Console.WriteLine(E.Message);
                Console.WriteLine(E.StackTrace);
                Console.WriteLine(Request);
                return 0;
            }
        }

        protected DataTable GetDataTable(string tableName, KeyValuePair<string, object> where)
        {
            DataTable dt = new DataTable();
            SQLiteCommand updatecmd = new SQLiteCommand()
            {
                CommandText = String.Format("SELECT * FROM {0} WHERE `{1}`=@{1};", tableName, where.Key)
            };
            var Con = GetNewConnection();
            updatecmd.Parameters.AddWithValue('@' + where.Key, where.Value);
            updatecmd.Connection = Con;
 

            try
            {
                Con.Open();
                SQLiteDataReader reader = updatecmd.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                Con.Close();
            }

            catch (Exception e)
            {
                //LOGME
                Console.WriteLine($"Line 160: {e.Message}");
            }

            return dt;
        }

        protected DataTable GetDataTable(SQLiteCommand cmd)
        {
            DataTable dt = new DataTable();
            var Con = GetNewConnection();
            cmd.Connection = Con;
            try
            {
                Con.Open();
                SQLiteDataReader reader = cmd.ExecuteReader();
                dt.Load(reader);
                reader.Close();
                Con.Close();
            }

            catch (Exception e)
            {
                //LOGME
                Console.WriteLine($"Line 181: {e.Message}");
            }

            return dt;
        }

        protected DataTable GetDataTable(String sql)
        {
            DataTable dt = new DataTable();
            var Con = GetNewConnection();

            try
            {

                    Con.Open();
                SQLiteCommand mycommand = new SQLiteCommand(Con)
                {
                    CommandText = sql
                };
                SQLiteDataReader reader = mycommand.ExecuteReader();
                    dt.Load(reader);
                    reader.Close();
                    Con.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine($"Line 198: {e.Message}");
                return new DataTable();
            }
            return dt;
        }

        protected String FileName;
    }

    public class ClientsDB : Database
    {
        public ClientsDB(String FN) : base(FN) { }

        public override void Init()
        {
            if (!File.Exists(FileName))
            {
                String Create = "CREATE TABLE [CLIENTS] ( [Name] TEXT  NULL, [npID] TEXT  NULL, [Number] INTEGER PRIMARY KEY AUTOINCREMENT, [Level] INT DEFAULT 0 NULL, [LastOffense] TEXT NULL, [Connections] INT DEFAULT 1 NULL, [IP] TEXT NULL, [LastConnection] TEXT NULL, [UID] TEXT NULL, [Masked] INT DEFAULT 0, [Reserved] INT DEFAULT 0);";
                ExecuteNonQuery(Create);
                Create = "CREATE TABLE [BANS] ( [TYPE] TEXT NULL, [Reason] TEXT NULL, [npID] TEXT NULL, [bannedByID] TEXT NULL, [IP] TEXT NULL, [TIME] TEXT NULL, [EXPIRES] TEXT);";
                ExecuteNonQuery(Create);
            }
        }


        public List<Player> GetRecentPlayers()
        {
            List<Player> returnssss = new List<Player>();
            //String Query = String.Format($"SELECT * FROM CLIENTS LIMIT 15 OFFSET (SELECT COUNT(*) FROM CLIENTS)-15");
            String Query = "SELECT * FROM CLIENTS ORDER BY LastConnection DESC LIMIT 25";
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    DateTime lastCon = DateTime.MinValue;
                    DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                    returnssss.Add(new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon, ResponseRow["UID"].ToString(), ResponseRow["Masked"].ToString() == "1"));
                }
            }

            return returnssss.OrderByDescending(p => p.LastConnection).ToList(); ;
        }

        public List<Player> GetPlayers(List<String> npIDs)
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

                    returnssss.Add(new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon, ResponseRow["UID"].ToString(), ResponseRow["Masked"].ToString() == "1"));
                }
            }

            return returnssss;
        }

        public List<Player> GetPlayers(List<int> databaseIDs)
        {
            List<Player> returnssss = new List<Player>();
            String Condition = String.Join("' OR Number = '", databaseIDs);

            String Query = String.Format("SELECT * FROM CLIENTS WHERE Number = '{0}'", Condition);
            DataTable Result = GetDataTable(Query);

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow ResponseRow in Result.Rows)
                {
                    DateTime lastCon = DateTime.MinValue;
                    DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                    returnssss.Add(new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), -1, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon, ResponseRow["UID"].ToString(), ResponseRow["Masked"].ToString() == "1"));
                }
            }

            return returnssss;
        }

        //Overloaded method for getPlayer, returns Client with matching DBIndex, null if none found
        public Player GetPlayer(int dbIndex)
        {
            DataTable Result = GetDataTable("CLIENTS", new KeyValuePair<string, object>("Number", dbIndex));

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

                return new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32(p["Connections"]), p["IP"].ToString(), LC, p["UID"].ToString(), p["Masked"].ToString() == "1");
            }

            else
                return null;
        }

        //get player by ip, (used for webfront)
        public Player GetPlayer(String IP)
        {
            DataTable Result = GetDataTable("CLIENTS", new KeyValuePair<string, object>("IP", IP));

            if (Result != null && Result.Rows.Count > 0)
            {
                List<Player> lastKnown = new List<Player>();
                foreach (DataRow p in Result.Rows)
                {
                    DateTime LC;
                    try
                    {
                        LC = DateTime.Parse(p["LastConnection"].ToString());
                        lastKnown.Add(new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32((DateTime.Now - LC).TotalSeconds), p["IP"].ToString(), LC, p["UID"].ToString(), p["Masked"].ToString() == "1"));
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

        //Returns a single player object with matching GUID, false if no matches
        public Player GetPlayer(String ID, int cNum)
        {
            DataTable Result = GetDataTable("CLIENTS", new KeyValuePair<string, object>("npID", ID));

            if (Result != null && Result.Rows.Count > 0)
            {
                DataRow ResponseRow = Result.Rows[0];
                DateTime lastCon = DateTime.MinValue;
                DateTime.TryParse(ResponseRow["LastConnection"].ToString(), out lastCon);

                return new Player(ResponseRow["Name"].ToString(), ResponseRow["npID"].ToString(), cNum, (Player.Permission)(ResponseRow["Level"]), Convert.ToInt32(ResponseRow["Number"]), ResponseRow["LastOffense"].ToString(), (int)ResponseRow["Connections"], ResponseRow["IP"].ToString(), lastCon, ResponseRow["UID"].ToString(), ResponseRow["Masked"].ToString() == "1");
            }

            else
                return null;
        }

        //Returns a list of players matching name parameter, null if no players found matching
        public List<Player> FindPlayers(String name)
        {
            var Con = GetNewConnection();
            SQLiteCommand cmd = new SQLiteCommand(Con)
            {
                CommandText = "SELECT * FROM CLIENTS WHERE Name LIKE @Name"
            };
            cmd.Parameters.AddWithValue("@Name", '%' + name + '%');

            var Result = GetDataTable(cmd);

            List<Player> Players = new List<Player>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                {
                    DateTime LC;
                    string Masked = null;
                    try
                    {
                        LC = DateTime.Parse(p["LastConnection"].ToString());
                        Masked = p["Masked"].ToString();

                    }
                    catch (Exception)
                    {
                        if (Masked == null)
                            Masked = "0";

                        LC = DateTime.MinValue;
                    }
                    Players.Add(new Player(p["Name"].ToString(), p["npID"].ToString(), -1, (Player.Permission)(p["Level"]), Convert.ToInt32(p["Number"]), p["LastOffense"].ToString(), Convert.ToInt32(p["Connections"]), p["IP"].ToString(), LC, p["IP"].ToString(), Masked == "1"));
                }
                return Players;
            }

            else
                return null;
        }

        //Returns any player with level 4 permissions, null if no owner found
        public Player GetOwner()
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

        public List<Penalty> GetClientPenalties(Player P)
        {
            List<Penalty> ClientPenalties = new List<Penalty>();
            String Query = $"SELECT * FROM `BANS` WHERE `npID` = '{P.NetworkID}' OR `IP` = '{P.IP}'";
            DataTable Result = GetDataTable(Query);

            foreach (DataRow Row in Result.Rows)
            {
                if (Row["TIME"].ToString().Length < 2) //compatibility with my old database
                    Row["TIME"] = DateTime.Now.ToString();

                Penalty.Type BanType = Penalty.Type.Ban;
                if (Row["TYPE"].ToString().Length != 0)
                    BanType = (Penalty.Type)Enum.Parse(typeof(Penalty.Type), Row["TYPE"].ToString());

                ClientPenalties.Add(new Penalty(BanType, Row["Reason"].ToString().Trim(), Row["npID"].ToString(), Row["bannedByID"].ToString(), DateTime.Parse(Row["TIME"].ToString()), Row["IP"].ToString(), DateTime.Parse(Row["EXPIRES"].ToString())));

            }

            return ClientPenalties;
        }

        public List<Penalty> GetPenaltiesChronologically(int offset, int count)
        {
            List<Penalty> ClientPenalties = new List<Penalty>();
            DataTable Result = GetDataTable($"SELECT * FROM BANS LIMIT {count} OFFSET (SELECT COUNT(*) FROM BANS)-{offset + 10}");

            foreach (DataRow Row in Result.Rows)
            {
                if (Row["TIME"].ToString().Length < 2) //compatibility with my old database
                    Row["TIME"] = DateTime.Now.ToString();

                var  BanType = (Penalty.Type)Enum.Parse(typeof(Penalty.Type), Row["TYPE"].ToString());
                ClientPenalties.Add(new Penalty(BanType, Row["Reason"].ToString().Trim(), Row["npID"].ToString(), Row["bannedByID"].ToString(), DateTime.Parse(Row["TIME"].ToString()), Row["IP"].ToString(), DateTime.Parse(Row["EXPIRES"].ToString())));
            }

            return ClientPenalties;
        }

        //Returns all players with level > Flagged
        public List<Player> GetAdmins()
        {
            List<Player> Admins = new List<Player>();
            String Query = String.Format("SELECT * FROM CLIENTS WHERE Level >= '{0}' ORDER BY Name", (int)Player.Permission.Trusted);
            DataTable Result = GetDataTable(Query);

            foreach (DataRow P in Result.Rows)
                Admins.Add(new Player(P["Name"].ToString(), P["npID"].ToString(), (Player.Permission)P["Level"], P["IP"].ToString(), P["UID"].ToString(), Convert.ToInt32(P["Number"].ToString())));

            return Admins;
        }

        //Returns total number of player entries in database
        public int TotalPlayers()
        {
            DataTable Result = GetDataTable("SELECT * from CLIENTS ORDER BY Number DESC LIMIT 1");
            if (Result.Rows.Count > 0)
                return Convert.ToInt32(Result.Rows[0]["Number"]);
            else
                return 0;
        }

        //Add specified player to database
        public void AddPlayer(Player P)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>
            {
                { "Name", Utilities.StripIllegalCharacters(P.Name) },
                { "npID", P.NetworkID },
                { "Level", (int)P.Level },
                { "LastOffense", "" },
                { "Connections", 1 },
                { "IP", P.IP },
                { "LastConnection", Utilities.DateTimeSQLite(DateTime.Now) },
                { "UID", P.UID },
                { "Masked", Convert.ToInt32(P.Masked) }
            };
            Insert("CLIENTS", newPlayer);
        }

        ///Update information of specified player
        public void UpdatePlayer(Player P)
        {
            Dictionary<String, Object> updatedPlayer = new Dictionary<String, Object>
            {
                { "Name", P.Name },
                { "npID", P.NetworkID },
                { "Level", (int)P.Level },
                { "LastOffense", P.lastOffense },
                { "Connections", P.Connections },
                { "IP", P.IP },
                { "LastConnection", Utilities.DateTimeSQLite(DateTime.Now) },
                { "UID", P.UID },
                { "Masked", Convert.ToInt32(P.Masked) }
            };
            Update("CLIENTS", updatedPlayer, new KeyValuePair<string, object>("npID", P.NetworkID));
        }


        //Add specified ban to database
        public void AddPenalty(Penalty B)
        {
            Dictionary<String, object> newBan = new Dictionary<String, object>
            {
                { "Reason", Utilities.StripIllegalCharacters(B.Reason) },
                { "npID", B.OffenderID },
                { "bannedByID", B.PenaltyOriginID },
                { "IP", B.IP },
                { "TIME", Utilities.DateTimeSQLite(DateTime.Now) },
                { "TYPE", B.BType },
                { "EXPIRES", B.Expires }
            };
            Insert("BANS", newBan);
        }


        //Deletes ban with matching GUID
        public void RemoveBan(String GUID)
        {
            String Query = String.Format("DELETE FROM BANS WHERE npID = '{0}'", GUID);
            ExecuteNonQuery(Query);
        }

        public void RemoveBan(String GUID, String IP)
        {
            String Query = String.Format("DELETE FROM BANS WHERE npID = '{0}' or IP = '{1}'", GUID, IP);
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

        public Aliases GetPlayerAliases(int dbIndex)
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

        public List<Aliases> GetPlayerAliases(String IP)
        {
            var Con = GetNewConnection();
            SQLiteCommand cmd = new SQLiteCommand(Con)
            {
                CommandText = "SELECT * FROM ALIASES WHERE IPS LIKE @IP"
            };
            cmd.Parameters.AddWithValue("@IP", IP);

            var Result = GetDataTable(cmd);

            List<Aliases> players = new List<Aliases>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                    players.Add(new Aliases(Convert.ToInt32(p["Number"]), p["NAMES"].ToString(), p["IPS"].ToString()));
            }

            return players;
        }

        public List<Aliases> FindPlayerAliases(String name)
        {
            name = name.Replace("'", "");
            String[] IP = name.Split('.');
            String DefaultIP = "LEGACY_INVALID_IP";
            if (IP.Length > 1)
                DefaultIP = (IP[0] + '.' + IP[1] + '.');
            var Con = GetNewConnection();

            SQLiteCommand cmd = new SQLiteCommand(Con)
            {
                CommandText = "SELECT * FROM ALIASES WHERE NAMES LIKE @name OR IPS LIKE @ip LIMIT 15"
            };
            cmd.Parameters.AddWithValue("@name", '%' + name + '%');
            cmd.Parameters.AddWithValue("@ip", '%' + DefaultIP + '%');

            var Result = GetDataTable(cmd);


            List<Aliases> players = new List<Aliases>();

            if (Result != null && Result.Rows.Count > 0)
            {
                foreach (DataRow p in Result.Rows)
                    players.Add(new Aliases(Convert.ToInt32(p["Number"]), p["NAMES"].ToString(), p["IPS"].ToString()));
            }

            return players;
        }

        public void AddPlayerAliases(Aliases Alias)
        {
            Dictionary<String, object> newPlayer = new Dictionary<String, object>
            {
                { "Number", Alias.Number },
                { "NAMES", Utilities.StripIllegalCharacters(String.Join(";", Alias.Names)) },
                { "IPS", String.Join(";", Alias.IPS) }
            };
            Insert("ALIASES", newPlayer);
        }

        public void UpdatePlayerAliases(Aliases Alias)
        {
            Dictionary<String, object> updatedPlayer = new Dictionary<String, object>
            {
                { "Number", Alias.Number },
                { "NAMES", String.Join(";", Alias.Names) },
                { "IPS", String.Join(";", Alias.IPS) }
            };
            Update("ALIASES", updatedPlayer, new KeyValuePair<string, object>("Number", Alias.Number));
        }
    }
}
