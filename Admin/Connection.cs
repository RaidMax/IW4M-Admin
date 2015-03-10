using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

namespace IW4MAdmin
{
    class Connection
    {
        public Connection(String Loc)
        {
            Location = Loc;
            ConnectionHandle = WebRequest.Create(Location);
            ConnectionHandle.Proxy = null;
        }

        public String Read()
        {
            try
            {
                WebResponse Resp = ConnectionHandle.GetResponse();
                StreamReader data_in = new StreamReader(Resp.GetResponseStream());
                String result = data_in.ReadToEnd();

                data_in.Close();
                Resp.Close();

                return result;
            }

            catch (System.Net.WebException)
            {
                return null;
            }
        }

        public void Request(String data)
        {
            try
            {
                WebResponse Resp = WebRequest.Create(data).GetResponse();     
                Resp.Close();
            }

            catch (System.Net.WebException)
            {
                return;
            }
        }

        private String Location;
        private WebRequest ConnectionHandle;
    }
}
